using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    public sealed class Hero : Actor
    {
        public HeroDef Def;
        public Controls Input;
        public int Slot;
        public float Hp;
        public bool Downed;
        public float ReviveProgress;      // 0..1 while an ally holds Interact on us
        public float AttackCd, SpecialCd;
        public Vector3 Facing = Vector3.right;
        public bool Carrying => Game.I.Lantern.Carrier == this;
        public float SpecialReady => Def.SpecialCooldown <= 0 ? 1 : 1 - Mathf.Clamp01(SpecialCd / Def.SpecialCooldown);

        const float ReviveTime = 1.6f, ReachLantern = 1.7f, ReachRevive = 1.7f;

        public static Hero Create(HeroKind kind, Controls input, int slot, Vector3 pos)
        {
            var go = new GameObject("Hero " + kind);
            go.transform.position = pos;
            var h = go.AddComponent<Hero>();
            h.Input = input;
            h.Slot = slot;
            h.SetKind(kind);
            return h;
        }

        public void SetKind(HeroKind kind)
        {
            Def = HeroDef.Get(kind);
            Hp = Def.MaxHp;
            name = "Hero " + kind;
            if (body == null) BuildVisual(PixelArt.Hero(kind));
            else { frames = PixelArt.Hero(kind); body.sprite = frames[0]; }
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.State == GameState.Defeat) { AnimateBody(false, roll: Downed ? 90 : 0); return; }
            float dt = Time.deltaTime;
            AttackCd -= dt; SpecialCd -= dt;

            if (Downed)
            {
                ReviveProgress = Mathf.Max(0, ReviveProgress - dt * 0.25f);
                Move(Vector3.zero);
                AnimateBody(false, roll: facingLeft ? -90 : 90);
                return;
            }

            // movement
            var m = Input.Move;
            var dir = new Vector3(m.x, 0, m.y);
            float speed = Def.Speed * (Carrying ? 0.85f : 1f);
            if (dir.sqrMagnitude > 0.01f)
            {
                Facing = dir.normalized;
                if (Mathf.Abs(m.x) > 0.2f) facingLeft = m.x < 0;
            }
            Move(dir * speed * dt);

            // lantern / revive
            bool reviving = false;
            if (Carrying)
            {
                if (Input.Interact) g.Lantern.Throw(Facing);
            }
            else
            {
                var ally = g.NearestDowned(this, ReachRevive);
                if (ally != null && Input.InteractHeld)
                {
                    reviving = true;
                    ally.ReviveProgress += dt / ReviveTime;
                    if (ally.ReviveProgress >= 1) ally.Revive(0.4f);
                }
                else if (Input.Interact && g.Lantern.CanPickUp(Pos, ReachLantern))
                    g.Lantern.PickUp(this);
            }

            // abilities (not while carrying the lantern or reviving; Special cycles hero in the lobby)
            if (g.State == GameState.Lobby && Input.Special) { g.CycleHero(this); }
            else if (!Carrying && !reviving)
            {
                if (Input.Attack && AttackCd <= 0) { AttackCd = Def.AttackCooldown; DoAttack(); }
                if (Input.Special && SpecialCd <= 0) { SpecialCd = Def.SpecialCooldown; DoSpecial(); }
            }

            AnimateBody(dir.sqrMagnitude > 0.01f);
        }

        protected override Color TintExtra(Color c)
        {
            if (Hp < Def.MaxHp * 0.25f && Mathf.Repeat(Time.time, 0.5f) < 0.15f) return Color.Lerp(c, Color.red, 0.5f);
            return c;
        }

        // ---------------------------------------------------------------- damage / revive

        public void Damage(float amount, Vector3 from)
        {
            if (Downed || Game.I.State != GameState.Playing) return;
            Hp -= amount;
            flashT = 0.12f;
            Push(Pos - from, 5f);
            Game.I.Shake(0.12f, 0.08f);
            if (Hp <= 0)
            {
                Hp = 0;
                Downed = true;
                ReviveProgress = 0;
                if (Carrying) Game.I.Lantern.Drop();
                Fx.Play("poof", Pos);
                Game.I.Popup(Pos + Vector3.up * 2, "DOWN!", new Color(1f, 0.4f, 0.4f));
            }
        }

        public void Heal(float amount)
        {
            if (Downed) return;
            Hp = Mathf.Min(Def.MaxHp, Hp + amount);
        }

        public void Revive(float fraction)
        {
            if (!Downed) return;
            Downed = false;
            ReviveProgress = 0;
            Hp = Def.MaxHp * fraction;
            Fx.Play("heal-aoe", Pos, scale: 0.45f);
            Game.I.Popup(Pos + Vector3.up * 2, "BACK UP", new Color(0.6f, 1f, 0.7f));
        }

        // ---------------------------------------------------------------- abilities

        Vector3 Muzzle => Pos + Vector3.up * 0.8f + Facing * 0.5f;

        void DoAttack()
        {
            var g = Game.I;
            switch (Def.Kind)
            {
                case HeroKind.Cinder:
                    Projectile.Fire(this, "fireball", Muzzle, AimDir(9f), 11f, 11f, 9f, Element.Fire);
                    break;
                case HeroKind.Rime:
                    Projectile.Fire(this, "ice-lance", Muzzle, AimDir(9f), 16f, 9f, 9f, Element.Ice);
                    break;
                case HeroKind.Volt:
                {
                    var e = g.NearestEnemy(Pos, 6.5f, Facing);
                    if (e != null)
                    {
                        Bolt.Draw(Muzzle, e.Pos + Vector3.up * e.Height * 0.5f, new Color(1f, 0.97f, 0.5f));
                        Fx.Play("zap-hit", e.Pos + Vector3.up * e.Height * 0.5f, scale: 0.7f);
                        e.TakeHit(new Hit(this, 8f, Element.Lightning, Pos) { StunTime = 0.35f });
                    }
                    else Fx.Play("zap-hit", Pos + Facing * 2f + Vector3.up * 0.8f, scale: 0.5f);
                    break;
                }
                case HeroKind.Sol:
                {
                    var e = g.NearestEnemy(Pos, 7f, Facing);
                    var at = e != null ? e.Pos : Pos + Facing * 3f;
                    Fx.Play("smite", at, scale: 0.75f);
                    StartCoroutine(After(0.25f, () => g.HitArea(this, at, 1.1f, new Hit(this, 14f, Element.Holy, at))));
                    break;
                }
            }
        }

        void DoSpecial()
        {
            var g = Game.I;
            switch (Def.Kind)
            {
                case HeroKind.Cinder:
                {
                    Fx.Play("fire-ring", Pos, scale: 0.9f);
                    var at = Pos;
                    StartCoroutine(After(Fx.ContactTime("fire-ring"), () =>
                    {
                        g.HitStop(0.08f); g.Shake(0.25f, 0.2f);
                        g.HitArea(this, at, 3.4f, new Hit(this, 22f, Element.Fire, at) { Push = 6f });
                    }));
                    break;
                }
                case HeroKind.Rime:
                {
                    Fx.Play("frost-nova", Pos, scale: 0.75f);
                    var at = Pos;
                    StartCoroutine(After(Fx.ContactTime("frost-nova"), () =>
                    {
                        g.HitStop(0.08f); g.Shake(0.22f, 0.18f);
                        g.HitArea(this, at, 3.8f, new Hit(this, 12f, Element.Ice, at) { FreezeTime = 2.5f, Push = 3f });
                    }));
                    break;
                }
                case HeroKind.Volt:
                    StartCoroutine(Chain());
                    break;
                case HeroKind.Sol:
                {
                    Fx.Play("heal-rose", Pos, scale: 0.8f);
                    var at = Pos;
                    float r = 4.6f;
                    foreach (var h in g.Heroes)
                    {
                        if ((h.Pos - at).sqrMagnitude > r * r) continue;
                        if (h.Downed) h.Revive(0.4f); else h.Heal(35f);
                    }
                    g.Popup(at + Vector3.up * 2.4f, "ROSE MEND", new Color(1f, 0.6f, 0.75f));
                    break;
                }
            }
        }

        IEnumerator Chain()
        {
            var g = Game.I;
            var hit = new List<Enemy>();
            Vector3 from = Muzzle;
            var first = g.NearestEnemy(Pos, 7.5f, Facing);
            if (first == null) { Fx.Play("zap-hit", Pos + Facing * 2.5f + Vector3.up * 0.8f); SpecialCd = 1f; yield break; }
            var cur = first;
            for (int i = 0; i < 5 && cur != null; i++)
            {
                hit.Add(cur);
                var to = cur.Pos + Vector3.up * cur.Height * 0.5f;
                Bolt.Draw(from, to, new Color(0.85f, 0.9f, 1f), 0.18f, 0.14f);
                Fx.Play("zap-hit", to);
                cur.TakeHit(new Hit(this, 16f, Element.Lightning, from) { StunTime = 1f });
                g.HitStop(0.03f);
                from = to;
                var last = cur;
                cur = g.NearestEnemy(last.Pos, 4.5f, Vector3.zero, hit);
                yield return new WaitForSeconds(0.09f);
            }
            g.Shake(0.2f, 0.15f);
        }

        /// <summary>Soft aim: snap toward the nearest enemy inside a cone in front of us.</summary>
        Vector3 AimDir(float range)
        {
            var e = Game.I.NearestEnemy(Pos, range, Facing, null, 0.55f);
            if (e == null) return Facing;
            var d = e.Pos - Pos; d.y = 0;
            return d.normalized;
        }

        static IEnumerator After(float t, System.Action a)
        {
            yield return new WaitForSeconds(t);
            a();
        }
    }
}
