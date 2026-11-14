using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>One hit of damage from a hero. Kindred Reactions are worked out in Enemy.TakeHit.</summary>
    public struct Hit
    {
        public Hero Source;
        public float Damage;
        public Element Element;
        public Vector3 From;
        public float StunTime, FreezeTime, Push;
        public bool NoReaction, Ult;
        public Hit(Hero src, float dmg, Element el, Vector3 from)
        { Source = src; Damage = dmg; Element = el; From = from; StunTime = 0; FreezeTime = 0; Push = 3f; NoReaction = false; Ult = false; }
    }

    /// <summary>A red warning on the floor that fills up, then deals damage to heroes standing inside. Every enemy attack is telegraphed so it can be dodged.</summary>
    public sealed class Telegraph : MonoBehaviour
    {
        public static readonly List<Telegraph> Active = new List<Telegraph>();
        float t, duration, radius, damage;
        SpriteRenderer fill;
        System.Action onFire;
        public Vector3 Pos => transform.position;
        public float Radius => radius;

        public static Telegraph Circle(Vector3 at, float radius, float windup, float damage, System.Action onFire = null)
        {
            var go = new GameObject("telegraph");
            go.transform.position = Util.Flat(at);
            var tg = go.AddComponent<Telegraph>();
            tg.radius = radius; tg.duration = windup; tg.damage = damage; tg.onFire = onFire;
            Util.FloorDecal(go.transform, Util.Blob(new Color(1f, 0.15f, 0.2f, 0.55f), 64, true), radius, -3);
            tg.fill = Util.FloorDecal(go.transform, Util.Blob(new Color(1f, 0.25f, 0.2f, 0.45f), 64), 0.01f, -2);
            Active.Add(tg);
            return tg;
        }

        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            t += Time.deltaTime;
            fill.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * Mathf.Clamp01(t / duration));
            if (t >= duration)
            {
                foreach (var h in Game.I.Heroes)
                    if (!h.Downed && (h.Pos - Pos).sqrMagnitude < (radius + 0.3f) * (radius + 0.3f))
                        h.Damage(damage, Pos);
                onFire?.Invoke();
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Ember shards (XP) and heart pickups that dropped enemies leave behind. They fly to the nearest hero.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public float Xp, Heal;
        float t;
        Vector3 vel;

        public static void Drop(Vector3 at, float xp, float heal)
        {
            var go = new GameObject(heal > 0 ? "heart" : "ember");
            go.transform.position = at + Vector3.up * 0.6f;
            var p = go.AddComponent<Pickup>();
            p.Xp = xp; p.Heal = heal;
            p.vel = new Vector3(Random.Range(-3f, 3f), 4f, Random.Range(-3f, 3f));
            Fx.Play(heal > 0 ? "heart-pickup" : "xp-orb", go.transform.position, follow: go.transform, scale: heal > 0 ? 0.7f : 0.55f, loop: true);
        }

        void Update()
        {
            t += Time.deltaTime;
            Hero target = null; float bd = float.MaxValue;
            foreach (var h in Game.I.Heroes)
            {
                if (h.Downed) continue;
                float d = (h.Pos - Util.Flat(transform.position)).sqrMagnitude;
                if (d < bd) { bd = d; target = h; }
            }
            if (t < 0.45f || target == null)
            {
                vel += Vector3.down * 12f * Time.deltaTime;
                transform.position += vel * Time.deltaTime;
                if (transform.position.y < 0.4f) { transform.position = new Vector3(transform.position.x, 0.4f, transform.position.z); vel *= 0.5f; vel.y = Mathf.Abs(vel.y) * 0.3f; }
                return;
            }
            var to = target.Pos + Vector3.up * 1f - transform.position;
            float speed = 6f + t * 14f;
            transform.position += to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
            if (to.magnitude < 0.5f)
            {
                if (Xp > 0) Game.I.GainXp(Xp);
                if (Heal > 0) foreach (var h in Game.I.Heroes) h.Heal(Heal);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>A hero projectile carrying a looping pixelfx billboard.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        Hero owner;
        Vector3 dir;
        float speed, damage, range, travelled;
        Element element;
        FxPlayer fx;
        bool hostile;

        public static void Fire(Hero owner, string fxName, Vector3 from, Vector3 dir, float speed, float damage, float range, Element el, float scale = 0.6f)
        {
            var p = Make(fxName, from, dir, scale);
            p.owner = owner; p.speed = speed; p.damage = damage; p.range = range; p.element = el;
        }

        /// <summary>An enemy bolt that hurts heroes.</summary>
        public static void Hostile(string fxName, Vector3 from, Vector3 dir, float speed, float damage, float scale = 0.5f)
        {
            var p = Make(fxName, from, dir, scale);
            p.hostile = true; p.speed = speed; p.damage = damage; p.range = 30f;
        }

        static Projectile Make(string fxName, Vector3 from, Vector3 dir, float scale)
        {
            var go = new GameObject("proj " + fxName);
            go.transform.position = from;
            var p = go.AddComponent<Projectile>();
            p.dir = dir.normalized;
            float roll = Fx.RollFor(p.dir, out bool flip);
            p.fx = Fx.Play(fxName, from, flip, roll, go.transform, scale, true);
            if (p.fx) p.fx.RollFrom = p.dir;
            return p;
        }

        void Update()
        {
            var g = Game.I;
            float step = speed * Time.deltaTime;
            transform.position += dir * step;
            travelled += step;
            var flat = Util.Flat(transform.position);

            if (hostile)
            {
                foreach (var h in g.Heroes)
                {
                    if (h.Downed || h.Invulnerable) continue;
                    if ((h.Pos - flat).sqrMagnitude < 0.55f * 0.55f) { h.Damage(damage, flat - dir); Fx.Play("hit-spark", transform.position, scale: 0.6f); Kill(); return; }
                }
            }
            else
            {
                foreach (var e in g.Enemies)
                {
                    if (e.Dead) continue;
                    if ((e.Pos - flat).sqrMagnitude < (e.Radius + 0.4f) * (e.Radius + 0.4f))
                    {
                        Fx.Play("hit-spark", transform.position, dir.x < 0, scale: 0.6f);
                        e.TakeHit(new Hit(owner, damage, element, flat - dir));
                        Kill();
                        return;
                    }
                }
            }
            if (travelled > range || flat.magnitude > Arena.Radius + 1) Kill();
        }

        void Kill()
        {
            if (fx) fx.Stop();
            Destroy(gameObject);
        }
    }

    /// <summary>A jagged lightning line that lives for a few frames.</summary>
    public sealed class Bolt : MonoBehaviour
    {
        float life;

        public static void Draw(Vector3 a, Vector3 b, Color c, float life = 0.1f, float width = 0.1f)
        {
            var go = new GameObject("bolt");
            go.AddComponent<Bolt>().life = life;
            var lr = go.AddComponent<LineRenderer>();
            if (Fx.SpriteMaterial) lr.sharedMaterial = Fx.SpriteMaterial;
            lr.startColor = lr.endColor = c;
            lr.startWidth = width; lr.endWidth = width * 0.6f;
            const int n = 9;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)(n - 1));
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.3f;
                lr.SetPosition(i, p);
            }
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0) Destroy(gameObject);
        }
    }

    /// <summary>
    /// The last ember: a spirit lantern that floats beside the party leader.
    /// Inside its light heroes regenerate and enemies are weakened; outside it enemies hit 50% harder.
    /// </summary>
    public sealed class Lantern : MonoBehaviour
    {
        public float BaseRadius = 7f;
        public float Radius => BaseRadius * Game.I.Party.LightRadius * (SolNear ? 1.25f : 1f);
        Light pointLight;
        Transform pool;
        float shown;
        bool SolNear;
        public Vector3 Pos => Util.Flat(transform.position);

        public static Lantern Create()
        {
            var go = new GameObject("Lantern");
            var l = go.AddComponent<Lantern>();
            var lightGo = new GameObject("Ember Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            l.pointLight = lightGo.AddComponent<Light>();
            l.pointLight.type = LightType.Point;
            l.pointLight.color = new Color(1f, 0.72f, 0.4f);
            l.pointLight.intensity = 2.4f;
            l.pointLight.shadows = LightShadows.Soft;
            var poolGo = new GameObject("Light Pool");
            l.pool = poolGo.transform;
            Util.FloorDecal(l.pool, Util.Blob(new Color(1f, 0.7f, 0.35f, 0.18f), 64), 1f, -8);
            // brass cage + ember
            var cage = new GameObject("Cage").transform;
            cage.SetParent(go.transform, false);
            Util.MeshObj("Top", Util.Frustum(0.16f, 0.05f, 0.12f, 8), cage, new Vector3(0, 0.2f, 0), Util.Lit(Util.Hex("#c9923a")));
            Util.MeshObj("Bottom", Util.Frustum(0.16f, 0.16f, 0.05f, 8), cage, new Vector3(0, -0.2f, 0), Util.Lit(Util.Hex("#c9923a")));
            Util.Prim(PrimitiveType.Sphere, cage, Vector3.zero, Vector3.one * 0.24f, Util.Glow(Util.Hex("#ffd27a")));
            Fx.Play("spirit-flame", go.transform.position, follow: go.transform, scale: 0.3f, loop: true);
            l.shown = l.BaseRadius;
            return l;
        }

        /// <summary>1 inside the light, fading to 0 past its edge.</summary>
        public float LightAt(Vector3 p)
        {
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(transform.position.x, transform.position.z));
            return 1f - Mathf.Clamp01((d - shown * 0.8f) / (shown * 0.3f));
        }

        void Update()
        {
            var g = Game.I;
            var lead = g.Leader;
            if (lead != null)
            {
                var want = lead.Pos + lead.transform.right * 0.8f - lead.transform.forward * 0.4f + Vector3.up * (2.3f + Mathf.Sin(Time.time * 2f) * 0.12f);
                transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
            }
            SolNear = g.Heroes.Exists(h => h.Def.Kind == HeroKind.Sol && !h.Downed && (h.Pos - Pos).sqrMagnitude < 25f);
            shown = Mathf.Lerp(shown, Radius, 1f - Mathf.Exp(-4f * Time.deltaTime));
            pool.position = Pos;
            pool.localScale = Vector3.one * shown * (1f + Mathf.PerlinNoise(Time.time * 2f, 0.4f) * 0.05f);
            pointLight.range = shown * 2.2f;
        }
    }
}
