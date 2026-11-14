using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>A hero's elemental firearm. Hitscan with spread, magazines, reloads and per-shot feedback.</summary>
    public sealed class WeaponDef
    {
        public string Name;
        public float Rps, Damage, HipSpread, AdsSpread, ReloadTime, Range = 30f, Kick;
        public int Mag, Pierce, Pellets = 1;
        public Color Tracer, Shell;
        public Element Element;
        public float ElementEvery = 1;      // apply the element every Nth hit (1 = every hit)

        public static WeaponDef For(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Cinder: return new WeaponDef { Name = "Emberline SMG", Rps = 12f, Damage = 6.5f, HipSpread = 6f, AdsSpread = 2.5f, ReloadTime = 1.5f, Mag = 45, Kick = 0.04f,
                        Tracer = Util.Hex("#ffb347"), Shell = Util.Hex("#d9a441"), Element = Element.Fire, ElementEvery = 3 };
                case HeroKind.Rime: return new WeaponDef { Name = "Glacier DMR", Rps = 3.2f, Damage = 30f, HipSpread = 3f, AdsSpread = 0.4f, ReloadTime = 2f, Mag = 12, Pierce = 2, Kick = 0.12f,
                        Tracer = Util.Hex("#bff1ff"), Shell = Util.Hex("#cfd8e0"), Element = Element.Ice };
                case HeroKind.Volt: return new WeaponDef { Name = "Arc Carbine", Rps = 9f, Damage = 8.5f, HipSpread = 4f, AdsSpread = 1.2f, ReloadTime = 1.4f, Mag = 30, Kick = 0.05f,
                        Tracer = Util.Hex("#fff35c"), Shell = Util.Hex("#c9b04a"), Element = Element.Lightning, ElementEvery = 4 };
                default: return new WeaponDef { Name = "Dawn Scattergun", Rps = 1.6f, Damage = 7f, HipSpread = 14f, AdsSpread = 8f, ReloadTime = 1.8f, Mag = 8, Pellets = 7, Range = 14f, Kick = 0.14f,
                        Tracer = Util.Hex("#ffd1e2"), Shell = Util.Hex("#c94a6a"), Element = Element.Holy, ElementEvery = 1 };
            }
        }
    }

    public sealed class Gun
    {
        public readonly WeaponDef Def;
        public int Ammo;
        public float ReloadLeft, Cooldown, Bloom;
        public int Shots, Hits;
        int elementCounter;
        public bool Reloading => ReloadLeft > 0;
        public int MagSize => Mathf.RoundToInt(Def.Mag * Game.I.Party.MagSize);

        public Gun(WeaponDef d) { Def = d; Ammo = d.Mag; }

        public void Tick(float dt)
        {
            Cooldown -= dt;
            Bloom = Mathf.MoveTowards(Bloom, 0, dt * 12f);
            if (ReloadLeft > 0)
            {
                ReloadLeft -= dt;
                if (ReloadLeft <= 0) Ammo = MagSize;
            }
        }

        public bool StartReload()
        {
            if (Reloading || Ammo >= MagSize) return false;
            ReloadLeft = Def.ReloadTime * Game.I.Party.ReloadTime;
            return true;
        }

        public void Refill() { Ammo = MagSize; ReloadLeft = 0; }

        /// <summary>Fires one trigger pull if ready. Returns true when a shot went out.</summary>
        public bool TryFire(Hero owner, Vector3 muzzle, Vector3 dir, float spreadMult)
        {
            if (Reloading || Cooldown > 0) return false;
            if (Ammo <= 0) { StartReload(); return false; }
            var P = Game.I.Party;
            Cooldown = 1f / (Def.Rps * P.FireRate);
            Ammo--;
            Shots++;
            float spread = Mathf.Lerp(Def.AdsSpread, Def.HipSpread, spreadMult) + Bloom;
            Bloom = Mathf.Min(Bloom + Def.Kick * 20f, 6f);
            int pellets = Def.Pellets + (Def.Pellets > 1 ? P.ExtraShots * 2 : P.ExtraShots);
            for (int i = 0; i < pellets; i++)
            {
                float a = Random.Range(-spread, spread) * (pellets > 1 ? 1f : Mathf.Lerp(0.3f, 1f, Random.value));
                if (pellets > 1 && Def.Pellets == 1) a += (i - (pellets - 1) / 2f) * 4f; // Twin Shot spreads extra bullets
                var d = Quaternion.Euler(0, a, 0) * dir;
                bool applyEl = ++elementCounter >= Def.ElementEvery;
                if (applyEl) elementCounter = 0;
                if (Shoot(owner, muzzle, d, applyEl)) Hits++;
            }
            ShotFx.Muzzle(muzzle, dir, Def.Tracer);
            ShotFx.Casing(muzzle - dir * 0.25f, owner.transform.right, Def.Shell);
            if (Game.I.Leader == owner) Game.I.Shake(0.05f, Def.Kick);
            if (Ammo <= 0) StartReload();
            return true;
        }

        bool Shoot(Hero owner, Vector3 muzzle, Vector3 dir, bool applyElement)
        {
            var g = Game.I;
            var P = g.Party;
            var origin = Util.Flat(muzzle);
            dir = Util.Flat(dir).normalized;
            float range = Def.Range;
            // blocked by pillars and the arena wall
            float wallT = range;
            foreach (var pl in g.World.Pillars)
            {
                float t = RayCircle(origin, dir, new Vector3(pl.x, 0, pl.y), pl.z);
                if (t >= 0 && t < wallT) wallT = t;
            }
            // enemies along the line, nearest first
            var hits = new List<(float t, Enemy e)>();
            foreach (var e in g.Enemies)
            {
                if (e.Dead) continue;
                float t = RayCircle(origin, dir, e.Pos, e.Radius + 0.15f);
                if (t >= 0 && t < wallT) hits.Add((t, e));
            }
            hits.Sort((a, b) => a.t.CompareTo(b.t));
            int pierce = Def.Pierce + P.Pierce;
            float endT = wallT;
            bool any = false;
            for (int i = 0; i < hits.Count && i <= pierce; i++)
            {
                var (t, e) = hits[i];
                endT = t;
                any = true;
                bool crit = Random.value < P.CritChance;
                float dmg = Def.Damage * P.Damage * (crit ? 2f : 1f) * (i > 0 ? 0.7f : 1f);
                var at = muzzle + dir * t;
                Fx.Play(crit ? "crit-hit" : "bullet-impact", at, dir.x > 0, scale: crit ? 0.5f : 0.35f);
                e.TakeHit(new Hit(owner, dmg, applyElement ? Def.Element : Element.None, at - dir) { Push = Def.Kick * 12f, Crit = crit });
                g.HitMarker(crit);
            }
            if (!any && wallT < range) Fx.Play("hit-flash", muzzle + dir * wallT, scale: 0.4f);
            ShotFx.Tracer(muzzle, muzzle + dir * (any ? endT : wallT), Def.Tracer);
            return any;
        }

        /// <summary>Distance along a flat ray to a circle, or -1.</summary>
        public static float RayCircle(Vector3 o, Vector3 d, Vector3 c, float r)
        {
            var oc = new Vector2(o.x - c.x, o.z - c.z);
            var dd = new Vector2(d.x, d.z);
            float b = Vector2.Dot(oc, dd), cc = oc.sqrMagnitude - r * r;
            float disc = b * b - cc;
            if (disc < 0) return -1;
            float t = -b - Mathf.Sqrt(disc);
            if (t < 0) t = -b + Mathf.Sqrt(disc);
            return t < 0 ? -1 : t;
        }
    }

    /// <summary>Per-shot juice: tracers, muzzle flashes and brass casings that bounce and settle on the floor.</summary>
    public static class ShotFx
    {
        static readonly Queue<Casing> casings = new Queue<Casing>();
        const int MaxCasings = 160;

        public static void Tracer(Vector3 a, Vector3 b, Color c)
        {
            var go = new GameObject("tracer");
            var lr = go.AddComponent<LineRenderer>();
            if (Fx.SpriteMaterial) lr.sharedMaterial = Fx.SpriteMaterial;
            lr.positionCount = 2;
            // a short bright streak toward the target plus a faint full-length line
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = 0.035f; lr.endWidth = 0.07f;
            lr.startColor = new Color(c.r, c.g, c.b, 0.15f); lr.endColor = new Color(1, 1, 1, 0.95f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Fade>().Init(lr, 0.06f);
        }

        public static void Muzzle(Vector3 at, Vector3 dir, Color c)
        {
            float roll = Fx.RollFor(dir, out bool flip);
            var fx = Fx.Play("muzzle-smg", at - dir * 0.15f, flip, roll, null, 0.45f);
            if (fx) fx.Lifetime = 0.07f;
            var glow = Util.Prim(PrimitiveType.Sphere, null, at, Vector3.one * 0.22f, Util.Glow(Color.Lerp(c, Color.white, 0.5f)));
            glow.AddComponent<Fade>().Init(null, 0.04f);
        }

        public static void Casing(Vector3 at, Vector3 right, Color c)
        {
            Casing cs;
            if (casings.Count >= MaxCasings) { cs = casings.Dequeue(); if (cs == null) cs = MakeCasing(c); }
            else cs = MakeCasing(c);
            cs.GetComponent<Renderer>().sharedMaterial = Util.Lit(c);
            cs.Launch(at, right * Random.Range(2f, 3.2f) + Vector3.up * Random.Range(2.5f, 3.5f) + Random.insideUnitSphere * 0.6f);
            casings.Enqueue(cs);
        }

        static Casing MakeCasing(Color c)
        {
            var go = Util.Prim(PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.035f, 0.035f, 0.09f), Util.Lit(c));
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.name = "casing";
            return go.AddComponent<Casing>();
        }
    }

    public sealed class Casing : MonoBehaviour
    {
        Vector3 vel, spin;
        int bounces;
        bool resting;

        public void Launch(Vector3 at, Vector3 v)
        {
            transform.position = at;
            vel = v; spin = Random.insideUnitSphere * 900f; bounces = 0; resting = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (resting) return;
            float dt = Time.deltaTime;
            vel += Vector3.down * 18f * dt;
            transform.position += vel * dt;
            transform.Rotate(spin * dt, Space.World);
            if (transform.position.y < 0.02f)
            {
                var p = transform.position; p.y = 0.02f; transform.position = p;
                bounces++;
                vel = new Vector3(vel.x * 0.5f, -vel.y * 0.35f, vel.z * 0.5f);
                spin *= 0.5f;
                if (bounces >= 3 || vel.magnitude < 0.4f)
                {
                    resting = true;
                    transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 90);
                }
            }
        }
    }

    /// <summary>Fades a line (or just waits) and then removes the object.</summary>
    public sealed class Fade : MonoBehaviour
    {
        LineRenderer lr; float life, t; Color a, b;
        public void Init(LineRenderer line, float seconds) { lr = line; life = seconds; if (lr) { a = lr.startColor; b = lr.endColor; } }
        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / life);
            if (lr) { lr.startColor = new Color(a.r, a.g, a.b, a.a * k); lr.endColor = new Color(b.r, b.g, b.b, b.a * k); }
            if (t >= life) Destroy(gameObject);
        }
    }
}
