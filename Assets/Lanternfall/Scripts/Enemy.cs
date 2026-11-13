using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    public enum EnemyKind { Shade, Skullwing, EliteShade }

    /// <summary>One hit of damage. Kindred Reactions are worked out in Enemy.TakeHit.</summary>
    public struct Hit
    {
        public Hero Source;
        public float Damage;
        public Element Element;
        public Vector3 From;
        public float StunTime, FreezeTime, Push;
        public bool NoReaction;
        public Hit(Hero src, float dmg, Element el, Vector3 from)
        { Source = src; Damage = dmg; Element = el; From = from; StunTime = 0; FreezeTime = 0; Push = 2.5f; NoReaction = false; }
    }

    public sealed class Enemy : Actor
    {
        public EnemyKind Kind;
        public float Hp, MaxHp, Speed, ContactDamage;
        float attackCd, wobble;

        // statuses (seconds left)
        public float Burning, Frozen, Stunned, Exposed;
        int chill;
        float burnTick;
        readonly Dictionary<string, FxPlayer> statusFx = new Dictionary<string, FxPlayer>();

        public bool Dead { get; private set; }
        bool Flying => Kind == EnemyKind.Skullwing;

        public static Enemy Spawn(EnemyKind kind, Vector3 pos, float hpScale)
        {
            var go = new GameObject("Enemy " + kind);
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            e.Kind = kind;
            switch (kind)
            {
                case EnemyKind.Shade:
                    e.MaxHp = 30; e.Speed = 2.3f; e.ContactDamage = 8; e.Radius = 0.45f; e.Height = 0.9f;
                    e.BuildVisual(PixelArt.Shade(false));
                    break;
                case EnemyKind.Skullwing:
                    e.MaxHp = 16; e.Speed = 3.8f; e.ContactDamage = 6; e.Radius = 0.4f; e.Height = 0.7f; e.hover = 1.1f;
                    e.BuildVisual(PixelArt.Skullwing(), 1f, 0.8f);
                    break;
                case EnemyKind.EliteShade:
                    e.MaxHp = 240; e.Speed = 1.7f; e.ContactDamage = 18; e.Radius = 0.9f; e.Height = 1.8f;
                    e.BuildVisual(PixelArt.Shade(true), 1.4f, 1.9f);
                    break;
            }
            e.MaxHp *= hpScale;
            e.Hp = e.MaxHp;
            e.wobble = Random.value * 10f;
            return e;
        }

        void Update()
        {
            var g = Game.I;
            float dt = Time.deltaTime;
            attackCd -= dt;
            TickStatuses(dt);
            if (Dead) return;

            bool held = Frozen > 0 || Stunned > 0 || g.State != GameState.Playing;
            Vector3 vel = Vector3.zero;
            var target = held ? null : PickTarget(g);
            if (target != null)
            {
                var to = target.Pos - Pos;
                float d = to.magnitude;
                if (d > 0.01f)
                {
                    var dir = to / d;
                    if (Flying) dir = Quaternion.Euler(0, Mathf.Sin(Time.time * 2 + wobble) * 35f, 0) * dir; // swoop
                    vel = dir * Speed * (Burning > 0 ? 1.15f : 1f);
                    if (Mathf.Abs(to.x) > 0.1f) facingLeft = to.x < 0;
                }
                if (d < Radius + target.Radius + 0.25f && attackCd <= 0)
                {
                    attackCd = 0.9f;
                    // Concept rule: enemies outside the Lantern's light hit harder.
                    float mult = g.Lantern.LightAt(Pos) < 0.5f ? 1.5f : 1f;
                    target.Damage(ContactDamage * mult, Pos);
                }
            }

            // keep a little personal space so packs spread out
            foreach (var o in g.Enemies)
            {
                if (o == this || o.Dead) continue;
                var away = Pos - o.Pos;
                float min = Radius + o.Radius;
                if (away.sqrMagnitude < min * min && away.sqrMagnitude > 0.0001f) vel += away.normalized * 2.5f;
            }
            if (Flying) { hover = 1.1f + Mathf.Sin(Time.time * 6 + wobble) * 0.12f; transform.position = Pos; }
            if (Flying) { var p = transform.position + vel * dt; p = g.Arena.Clamp(p, Radius); transform.position = p; knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt); transform.position += knock * dt; }
            else Move(vel * dt);

            AnimateBody(vel.sqrMagnitude > 0.1f || Flying, Flying ? 0.09f : 0.22f);
        }

        Hero PickTarget(Game g)
        {
            if (Kind == EnemyKind.Skullwing && g.Lantern.Carrier != null && !g.Lantern.Carrier.Downed) return g.Lantern.Carrier;
            Hero best = null; float bd = float.MaxValue;
            foreach (var h in g.Heroes)
            {
                if (h.Downed) continue;
                float d = (h.Pos - Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        protected override Color TintExtra(Color c)
        {
            if (Frozen > 0) return Color.Lerp(c, new Color(0.6f, 0.9f, 1f), 0.6f);
            if (Exposed > 0) return Color.Lerp(c, new Color(1f, 0.9f, 0.6f), 0.25f);
            return c;
        }

        // ---------------------------------------------------------------- hits, statuses, Kindred Reactions

        public void TakeHit(Hit h)
        {
            if (Dead) return;
            var g = Game.I;
            float dmg = h.Damage;

            if (!h.NoReaction)
            {
                // SHATTER: fire or lightning hits a Frozen target.
                if (Frozen > 0 && (h.Element == Element.Fire || h.Element == Element.Lightning))
                {
                    Frozen = 0; chill = 0;
                    dmg *= 2.5f;
                    Fx.Play("ice-shatter", Pos, scale: 0.8f);
                    g.Reaction("SHATTER", Pos, new Color(0.6f, 0.95f, 1f));
                    g.HitArea(h.Source, Pos, 2.2f, new Hit(h.Source, h.Damage * 0.8f, Element.Ice, Pos) { NoReaction = true, Push = 5f }, this);
                }
                // OVERLOAD: lightning hits a Burning target.
                else if (Burning > 0 && h.Element == Element.Lightning)
                {
                    Burning = 0;
                    Fx.Play("plasma-explosion", Pos + Vector3.up * 0.6f, scale: 0.8f);
                    g.Reaction("OVERLOAD", Pos, new Color(1f, 0.75f, 0.3f));
                    g.HitArea(h.Source, Pos, 2.8f, new Hit(h.Source, 20f, Element.None, Pos) { NoReaction = true, StunTime = 1.2f, Push = 7f });
                }
            }

            if (Dead) return; // a reaction blast can finish us off
            if (Exposed > 0) dmg *= 1.25f;
            Hp -= dmg;
            flashT = 0.08f;
            Push(Pos - h.From, h.Push * (Kind == EnemyKind.EliteShade ? 0.3f : 1f));
            g.DamageNumber(Pos + Vector3.up * (Height + hover + 0.4f), dmg);

            switch (h.Element)
            {
                case Element.Fire: Burning = Mathf.Max(Burning, 3f); break;
                case Element.Ice:
                    if (++chill >= 3) { chill = 0; h.FreezeTime = Mathf.Max(h.FreezeTime, 2f); }
                    break;
                case Element.Holy: Exposed = Mathf.Max(Exposed, 4f); break;
            }
            if (h.FreezeTime > 0) Frozen = Mathf.Max(Frozen, h.FreezeTime * (Kind == EnemyKind.EliteShade ? 0.5f : 1f));
            if (h.StunTime > 0) Stunned = Mathf.Max(Stunned, h.StunTime);

            if (Hp <= 0) Die();
        }

        void TickStatuses(float dt)
        {
            if (Burning > 0 && !Dead)
            {
                burnTick += dt;
                if (burnTick >= 0.5f)
                {
                    burnTick = 0;
                    Hp -= 2.5f;
                    if (Hp <= 0) { Die(); return; }
                }
            }
            Burning -= dt; Frozen -= dt; Stunned -= dt; Exposed -= dt;
            Status("burning", Burning > 0, 0f);
            Status("frozen", Frozen > 0, 0f);
            Status("stunned", Stunned > 0 && Frozen <= 0, Height + 0.1f);
        }

        void Status(string fx, bool on, float y)
        {
            statusFx.TryGetValue(fx, out var p);
            if (on && p == null)
            {
                float s = Kind == EnemyKind.EliteShade ? 1.3f : Kind == EnemyKind.Skullwing ? 0.6f : 0.8f;
                statusFx[fx] = Fx.Play(fx, transform.position + Vector3.up * (y + hover) + Vector3.back * 0.05f, follow: transform, scale: s, loop: true);
            }
            else if (!on && p != null) { p.Stop(); statusFx.Remove(fx); }
        }

        void Die()
        {
            if (Dead) return;
            Dead = true;
            foreach (var p in statusFx.Values) if (p) p.Stop();
            if (Kind == EnemyKind.EliteShade) { Fx.Play("dark-explosion", Pos, scale: 0.9f); Game.I.Shake(0.35f, 0.3f); Game.I.HitStop(0.1f); }
            else Fx.Play("poof", Pos + Vector3.up * hover, scale: Flying ? 0.6f : 0.8f);
            Game.I.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
