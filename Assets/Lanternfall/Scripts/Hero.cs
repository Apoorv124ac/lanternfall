using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>
    /// A Lanternbearer. Driven by a human (Controls) or by the companion AI when Input is null,
    /// so a solo player always fights alongside the other three heroes.
    /// </summary>
    public sealed class Hero : MonoBehaviour
    {
        public HeroDef Def;
        public Controls Input;           // null = AI companion
        public float Hp, UltCharge;
        public bool Downed, Invulnerable;
        public float ReviveProgress, DownedTime;
        public float AttackCd, SpecialCd, DodgeCd;
        public Vector3 Facing = Vector3.forward;
        public float Radius = 0.45f;
        public Humanoid Body { get; private set; }

        public bool IsAI => Input == null;
        public float MaxHp => Def.MaxHp + Game.I.Party.MaxHpBonus;
        public Vector3 Pos => Util.Flat(transform.position);
        public float SpecialReady => 1f - Mathf.Clamp01(SpecialCd / (Def.SpecialCooldown * Game.I.Party.Cooldown));

        PartyStats P => Game.I.Party;
        Pose pose; float poseT, poseDur;
        float hitFlash, aimHold, dodgeT, speedNow;
        Vector3 dodgeDir, knock;
        float aiThink, aiStrafe;
        Vector3 aiMove, aiAim;
        bool aiAttack, aiSpecial, aiUlt, aiDodge, aiRevive;

        const float ReviveTime = 1.4f, ReviveReach = 2f, DodgeTime = 0.42f, DodgeSpeed = 11f, SelfReviveTime = 10f;

        public static Hero Create(HeroKind kind, Controls input, Vector3 pos)
        {
            var go = new GameObject("Hero " + kind);
            go.transform.position = pos;
            var h = go.AddComponent<Hero>();
            h.Def = HeroDef.Get(kind);
            h.Input = input;
            h.Body = CharacterArt.BuildHero(go.transform, h.Def);
            h.Hp = h.Def.MaxHp;
            h.aiStrafe = Random.value * 10f;
            Util.FloorDecal(go.transform, Util.Blob(new Color(0, 0, 0, 0.45f), 32), 0.55f, -6);
            return h;
        }

        void Update()
        {
            var g = Game.I;
            float dt = Time.deltaTime;
            AttackCd -= dt; SpecialCd -= dt; DodgeCd -= dt; aimHold -= dt; hitFlash -= dt;
            if (pose != Pose.None) { poseT += dt / poseDur; if (poseT >= 1) pose = Pose.None; }

            if (g.State != GameState.Playing) { Animate(dt, 0); return; }

            if (Downed)
            {
                DownedTime += dt;
                ReviveProgress = Mathf.Max(0, ReviveProgress - dt * 0.2f);
                if (DownedTime > SelfReviveTime) Revive(0.3f);   // forgiving: you always get back up
                Animate(dt, 0);
                return;
            }

            // regen in the Lantern's light
            if (g.Lantern.LightAt(Pos) > 0.5f) Heal(P.LightRegen * dt, false);

            // ---- intent
            Vector3 move; Vector3 aim; bool attack, special, ult, dodge, revive;
            if (IsAI) { Think(dt); move = aiMove; aim = aiAim; attack = aiAttack; special = aiSpecial; ult = aiUlt; dodge = aiDodge; revive = aiRevive; }
            else
            {
                var m = Input.Move;
                var camYaw = Quaternion.Euler(0, g.CamRig.Yaw, 0);
                move = camYaw * new Vector3(m.x, 0, m.y);
                aim = g.Leader == this ? g.CamRig.AimDirection(Pos) : SoftAim(move.sqrMagnitude > 0.01f ? move.normalized : Facing, 14f);
                attack = Input.Attack; special = Input.Special; ult = Input.Ultimate; dodge = Input.Dodge; revive = Input.Interact;
            }

            // ---- dodge roll (invulnerable)
            if (dodge && DodgeCd <= 0 && dodgeT <= 0)
            {
                dodgeDir = move.sqrMagnitude > 0.01f ? move.normalized : -Facing;
                dodgeT = DodgeTime; DodgeCd = 0.9f * P.DodgeCd;
                Invulnerable = true;
                SetPose(Pose.Dodge, DodgeTime);
            }
            Vector3 vel;
            if (dodgeT > 0)
            {
                dodgeT -= dt;
                vel = dodgeDir * DodgeSpeed;
                if (dodgeT <= 0) Invulnerable = false;
            }
            else vel = move * Def.Speed * P.MoveSpeed * (aimHold > 0 ? 0.85f : 1f);

            knock = Vector3.MoveTowards(knock, Vector3.zero, 20f * dt);
            var p = transform.position + (vel + knock) * dt;
            // shoulder past enemies
            foreach (var e in g.Enemies)
            {
                var d = Util.Flat(p - e.Pos); float min = Radius + e.Radius;
                if (d.sqrMagnitude < min * min && d.sqrMagnitude > 0.0001f) p += d.normalized * (min - d.magnitude) * 0.5f;
            }
            transform.position = g.World.Resolve(p, Radius);
            speedNow = Util.Flat(vel).magnitude;

            // ---- facing
            if (aimHold > 0 && aim.sqrMagnitude > 0.01f) Facing = aim.normalized;
            else if (move.sqrMagnitude > 0.01f && dodgeT <= 0) Facing = move.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Facing), 1f - Mathf.Exp(-14f * dt));

            // ---- revive allies
            bool reviving = false;
            if (revive)
            {
                foreach (var h in g.Heroes)
                    if (h != this && h.Downed && (h.Pos - Pos).sqrMagnitude < ReviveReach * ReviveReach)
                    {
                        reviving = true;
                        h.ReviveProgress += dt / ReviveTime;
                        if (h.ReviveProgress >= 1) h.Revive(0.5f);
                        break;
                    }
            }

            // ---- abilities
            if (dodgeT <= 0 && !reviving)
            {
                if (ult && UltCharge >= 100) { UltCharge = 0; aimHold = 1f; Facing = aim.sqrMagnitude > 0.01f ? aim.normalized : Facing; StartCoroutine(Ultimate(aim)); }
                else if (special && SpecialCd <= 0) { SpecialCd = Def.SpecialCooldown * P.Cooldown; aimHold = 0.5f; Special(aim); }
                else if (attack && AttackCd <= 0) { AttackCd = Def.AttackCooldown / P.AttackSpeed; aimHold = 0.45f; Attack(aim); }
            }

            Animate(dt, speedNow);
        }

        void Animate(float dt, float speed)
        {
            Body.Animate(dt, speed, pose, poseT, Downed ? 1 : 0);
            if (hitFlash > 0) Body.Tint(new Color(1f, 0.55f, 0.55f));
            else if (Invulnerable) Body.Tint(new Color(0.8f, 0.9f, 1f));
            else Body.Tint(Color.clear);
        }

        void SetPose(Pose p, float dur) { pose = p; poseT = 0; poseDur = dur; }

        // ---------------------------------------------------------------- damage / healing

        public void Damage(float amount, Vector3 from)
        {
            if (Downed || Invulnerable || Game.I.State != GameState.Playing) return;
            if (IsAI) amount *= 0.75f;
            Hp -= amount;
            hitFlash = 0.12f;
            knock += Util.Flat(Pos - from).normalized * 4f;
            if (pose == Pose.None) SetPose(Pose.Hit, 0.3f);
            if (Game.I.Leader == this) Game.I.Shake(0.15f, 0.12f);
            if (Hp <= 0)
            {
                Hp = 0; Downed = true; ReviveProgress = 0; DownedTime = 0; Invulnerable = false; dodgeT = 0;
                Fx.Play("poof", Pos);
                Game.I.Popup(Pos + Vector3.up * 2.2f, Def.Name + " is down!", new Color(1f, 0.45f, 0.45f));
            }
        }

        public void Heal(float amount, bool show = true)
        {
            if (Downed || amount <= 0) return;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            if (show && Hp - before >= 1) Game.I.Popup(Pos + Vector3.up * 2.1f, "+" + Mathf.RoundToInt(Hp - before), new Color(0.5f, 1f, 0.6f), 13);
        }

        public void Revive(float fraction)
        {
            if (!Downed) return;
            Downed = false; ReviveProgress = 0;
            Hp = MaxHp * fraction;
            Invulnerable = false;
            Fx.Play("heal-rose", Pos, scale: 0.5f);
            Game.I.Popup(Pos + Vector3.up * 2.2f, Def.Name + " is back!", new Color(0.6f, 1f, 0.7f));
        }

        /// <summary>Called when this hero's hit lands: charges the ultimate, applies lifesteal.</summary>
        public void OnDealt(float dmg, bool ult)
        {
            if (!ult) UltCharge = Mathf.Min(100, UltCharge + dmg * 0.3f * P.UltRate);
            if (P.Lifesteal > 0) Heal(dmg * P.Lifesteal, false);
        }

        public void FullRestore()
        {
            Downed = false; Hp = MaxHp; ReviveProgress = 0; SpecialCd = 0; Invulnerable = false; dodgeT = 0;
        }

        // ---------------------------------------------------------------- aiming

        Vector3 Hand => (Body.HandR ? Body.HandR.position : Pos + Vector3.up * 1.2f) + Facing * 0.3f;
        Vector3 Chest => Pos + Vector3.up * 1.25f;

        /// <summary>Snap toward the enemy nearest the given direction (inside a cone).</summary>
        public Vector3 SoftAim(Vector3 dir, float range, float coneDeg = 40f)
        {
            Enemy best = null; float bestScore = float.MaxValue;
            foreach (var e in Game.I.Enemies)
            {
                if (e.Dead) continue;
                var to = e.Pos - Pos; float d = to.magnitude;
                if (d > range || d < 0.01f) continue;
                float ang = Vector3.Angle(dir, to);
                if (ang > coneDeg) continue;
                float score = ang * 0.08f + d;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best == null ? dir : Util.Flat(best.Pos - Pos).normalized;
        }

        float Dmg(float baseDmg) => baseDmg * P.Damage;

        // ---------------------------------------------------------------- abilities

        void Attack(Vector3 aim)
        {
            var g = Game.I;
            if (aim.sqrMagnitude < 0.01f) aim = Facing;
            SetPose(Pose.Cast, 0.32f);
            int shots = 1 + P.ExtraShots;
            switch (Def.Kind)
            {
                case HeroKind.Cinder:
                case HeroKind.Rime:
                {
                    bool fire = Def.Kind == HeroKind.Cinder;
                    for (int i = 0; i < shots; i++)
                    {
                        float spread = (i - (shots - 1) / 2f) * 9f;
                        var dir = Quaternion.Euler(0, spread, 0) * aim;
                        Projectile.Fire(this, fire ? "fireball" : "ice-lance", Chest + aim * 0.6f, dir, fire ? 18f : 22f, Dmg(fire ? 14f : 11f), 18f, fire ? Element.Fire : Element.Ice, 0.55f);
                    }
                    break;
                }
                case HeroKind.Volt:
                {
                    var hit = new List<Enemy>();
                    for (int i = 0; i < shots; i++)
                    {
                        var e = g.NearestEnemy(Pos, 11f, aim, hit, 0.3f);
                        if (e == null) break;
                        hit.Add(e);
                        var at = e.Pos + Vector3.up * e.Height * 0.55f;
                        Bolt.Draw(Hand, at, new Color(1f, 0.97f, 0.55f), 0.09f, 0.08f);
                        Fx.Play("zap-hit", at, scale: 0.55f);
                        e.TakeHit(new Hit(this, Dmg(10f), Element.Lightning, Pos) { StunTime = 0.3f });
                    }
                    if (hit.Count == 0) Fx.Play("zap-hit", Chest + aim * 3f, scale: 0.4f);
                    break;
                }
                case HeroKind.Sol:
                {
                    var hit = new List<Enemy>();
                    for (int i = 0; i < shots; i++)
                    {
                        var e = g.NearestEnemy(Pos, 12f, aim, hit, 0.3f);
                        var at = e != null ? e.Pos : Pos + aim * 5f;
                        if (e != null) hit.Add(e);
                        Fx.Play("smite", at, scale: 0.6f);
                        StartCoroutine(After(0.22f, () => g.HitArea(this, at, 1.3f * P.Area, new Hit(this, Dmg(17f), Element.Holy, at))));
                        if (e == null) break;
                    }
                    break;
                }
            }
        }

        void Special(Vector3 aim)
        {
            var g = Game.I;
            SetPose(Pose.Special, 0.6f);
            var at = Pos;
            switch (Def.Kind)
            {
                case HeroKind.Cinder:
                    Fx.Play("fire-ring", at, scale: 0.85f * P.Area);
                    StartCoroutine(After(Fx.ContactTime("fire-ring"), () =>
                    {
                        g.HitStop(0.06f); if (g.Leader == this) g.Shake(0.25f, 0.2f);
                        g.HitArea(this, at, 4f * P.Area, new Hit(this, Dmg(30f), Element.Fire, at) { Push = 7f });
                    }));
                    break;
                case HeroKind.Rime:
                    Fx.Play("frost-nova", at, scale: 0.75f * P.Area);
                    StartCoroutine(After(Fx.ContactTime("frost-nova"), () =>
                    {
                        g.HitStop(0.06f); if (g.Leader == this) g.Shake(0.22f, 0.18f);
                        g.HitArea(this, at, 4.4f * P.Area, new Hit(this, Dmg(16f), Element.Ice, at) { FreezeTime = 2.6f, Push = 3f });
                    }));
                    break;
                case HeroKind.Volt:
                    StartCoroutine(Chain(aim));
                    break;
                case HeroKind.Sol:
                {
                    Fx.Play("heal-rose", at, scale: 0.9f * P.Area);
                    float r = 6f * P.Area;
                    foreach (var h in g.Heroes)
                    {
                        if ((h.Pos - at).sqrMagnitude > r * r) continue;
                        if (h.Downed) h.Revive(0.5f); else h.Heal(45f);
                    }
                    break;
                }
            }
        }

        IEnumerator Chain(Vector3 aim)
        {
            var g = Game.I;
            var hit = new List<Enemy>();
            Vector3 from = Hand;
            var cur = g.NearestEnemy(Pos, 12f, aim, null, 0f);
            if (cur == null) { Fx.Play("zap-hit", Chest + aim * 3f); SpecialCd = 1f; yield break; }
            int jumps = 5 + P.ExtraShots * 2;
            for (int i = 0; i < jumps && cur != null; i++)
            {
                hit.Add(cur);
                var to = cur.Pos + Vector3.up * cur.Height * 0.55f;
                Bolt.Draw(from, to, new Color(0.85f, 0.9f, 1f), 0.18f, 0.14f);
                Fx.Play("zap-hit", to);
                cur.TakeHit(new Hit(this, Dmg(24f), Element.Lightning, from) { StunTime = 1f });
                g.HitStop(0.025f);
                from = to;
                var last = cur;
                cur = g.NearestEnemy(last.Pos, 6f, Vector3.zero, hit);
                yield return new WaitForSeconds(0.08f);
            }
        }

        IEnumerator Ultimate(Vector3 aim)
        {
            var g = Game.I;
            SetPose(Pose.Ultimate, 1.0f);
            g.Popup(Pos + Vector3.up * 2.6f, Def.UltName.ToUpper() + "!", Def.Accent, 26);
            if (g.Leader == this) g.Shake(0.3f, 0.15f);
            Fx.Play("magic-circle-cast", Pos, scale: 0.7f);
            yield return new WaitForSeconds(0.35f);
            switch (Def.Kind)
            {
                case HeroKind.Cinder: // Meteor Rain
                    for (int i = 0; i < 8 + P.ExtraShots * 2; i++)
                    {
                        var e = g.RandomEnemy(Pos, 18f);
                        var at = e != null ? e.Pos : Pos + Quaternion.Euler(0, Random.Range(-40f, 40f), 0) * aim * Random.Range(3f, 9f);
                        Fx.Play("meteor", at, Random.value < 0.5f, scale: 0.9f);
                        StartCoroutine(After(0.62f, () =>
                        {
                            g.Shake(0.2f, 0.25f);
                            g.HitArea(this, at, 2.8f * P.Area, new Hit(this, Dmg(48f), Element.Fire, at) { Push = 8f, Ult = true });
                        }));
                        yield return new WaitForSeconds(0.22f);
                    }
                    break;
                case HeroKind.Rime: // Blizzard: a freezing vortex that drags enemies in
                {
                    var tgt = g.NearestEnemy(Pos, 14f, aim, null, 0f);
                    var at = tgt != null ? tgt.Pos : Pos + aim * 6f;
                    var fx = Fx.Play("blizzard-vortex", at, scale: 2.2f * P.Area, loop: true);
                    for (int i = 0; i < 12; i++)
                    {
                        foreach (var e in g.Enemies.ToArray())
                        {
                            if (e.Dead) continue;
                            var d = at - e.Pos;
                            if (d.magnitude < 7f * P.Area) e.Push(d, 6f);
                        }
                        g.HitArea(this, at, 5.5f * P.Area, new Hit(this, Dmg(12f), Element.Ice, at) { FreezeTime = i % 4 == 3 ? 2.5f : 0, Push = 0, Ult = true, NoReaction = i % 4 != 3 });
                        yield return new WaitForSeconds(0.45f);
                    }
                    if (fx) fx.Stop();
                    break;
                }
                case HeroKind.Volt: // Thunder God
                {
                    var hit = new List<Enemy>();
                    for (int i = 0; i < 10 + P.ExtraShots * 2; i++)
                    {
                        var e = g.NearestEnemy(Pos, 20f, Vector3.zero, hit);
                        if (e == null) break;
                        hit.Add(e);
                        var at = e.Pos;
                        Fx.Play("lightning-strike", at, scale: 0.9f);
                        StartCoroutine(After(0.15f, () =>
                        {
                            g.Shake(0.15f, 0.2f);
                            g.HitArea(this, at, 1.8f * P.Area, new Hit(this, Dmg(55f), Element.Lightning, at) { StunTime = 1.5f, Ult = true });
                        }));
                        yield return new WaitForSeconds(0.13f);
                    }
                    break;
                }
                case HeroKind.Sol: // Dawnbreak: revive and heal everyone, burn back the dark
                    Fx.Play("sunburst", Pos + Vector3.up * 1.2f, scale: 3f);
                    Fx.Play("heal-rose", Pos, scale: 1.4f);
                    foreach (var h in g.Heroes) { if (h.Downed) h.Revive(1f); else h.Heal(h.MaxHp); }
                    g.HitStop(0.08f); g.Shake(0.3f, 0.25f);
                    g.HitArea(this, Pos, 10f * P.Area, new Hit(this, Dmg(40f), Element.Holy, Pos) { StunTime = 2f, Push = 6f, Ult = true });
                    break;
            }
        }

        static IEnumerator After(float t, System.Action a)
        {
            yield return new WaitForSeconds(t);
            a();
        }

        // ---------------------------------------------------------------- companion AI

        void Think(float dt)
        {
            var g = Game.I;
            aiThink -= dt;
            aiDodge = false;
            var lead = g.Leader;

            // 1. step out of telegraphed attacks
            foreach (var tg in Telegraph.Active)
            {
                var d = Pos - tg.Pos; d.y = 0;
                if (d.magnitude < tg.Radius + 0.7f)
                {
                    aiMove = (d.sqrMagnitude < 0.01f ? transform.right : d.normalized);
                    aiDodge = DodgeCd <= 0 && Random.value < 0.05f;
                    aiAttack = false; aiSpecial = false; aiRevive = false;
                    return;
                }
            }

            if (aiThink > 0) return;
            aiThink = 0.15f;
            aiAttack = aiSpecial = aiUlt = aiRevive = false;
            aiMove = Vector3.zero;

            // 2. revive a downed ally
            Hero down = null; float dd = float.MaxValue;
            foreach (var h in g.Heroes)
                if (h != this && h.Downed) { float d = (h.Pos - Pos).sqrMagnitude; if (d < dd) { dd = d; down = h; } }
            if (down != null && Hp > MaxHp * 0.3f)
            {
                if (Def.Kind == HeroKind.Sol && SpecialCd <= 0 && dd < 30f) { aiSpecial = true; aiAim = Facing; return; }
                if (dd > 1.5f * 1.5f) aiMove = Util.Flat(down.Pos - Pos).normalized;
                else aiRevive = true;
                return;
            }

            // 3. fight
            var target = g.NearestEnemy(lead != null && !lead.Downed ? lead.Pos : Pos, 16f, Vector3.zero);
            if (target != null && (target.Pos - Pos).magnitude < 20f)
            {
                var to = Util.Flat(target.Pos - Pos); float d = to.magnitude; var dir = to / Mathf.Max(d, 0.01f);
                float range = Def.PreferredRange;
                var side = Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * 0.7f + aiStrafe);
                if (d > range + 1f) aiMove = dir + side * 0.3f;
                else if (d < range - 2f && Def.Kind != HeroKind.Rime) aiMove = -dir + side * 0.5f;
                else aiMove = side * 0.6f;
                aiAim = dir;
                aiAttack = d < range + 6f;

                int near = g.CountEnemies(Pos, 4.5f * P.Area);
                switch (Def.Kind)
                {
                    case HeroKind.Cinder: aiSpecial = near >= 2; if (near < 2 && SpecialCd <= 0 && g.CountEnemies(target.Pos, 4f) >= 3) aiMove = dir; break;
                    case HeroKind.Rime: aiSpecial = near >= 2 || (near >= 1 && Hp < MaxHp * 0.5f); break;
                    case HeroKind.Volt: aiSpecial = g.CountEnemies(target.Pos, 6f) >= 2 || target.Kind != EnemyKind.Cultist; break;
                    case HeroKind.Sol: aiSpecial = g.Heroes.Exists(h => !h.Downed && h.Hp < h.MaxHp * 0.55f && (h.Pos - Pos).sqrMagnitude < 36f); break;
                }
                aiUlt = UltCharge >= 100 && (g.CountEnemies(Pos, 14f) >= 4 || g.BossAlive || (Def.Kind == HeroKind.Sol && g.Heroes.FindAll(h => h.Downed || h.Hp < h.MaxHp * 0.4f).Count >= 2));
            }

            // 4. stay with the leader
            if (lead != null && lead != this)
            {
                var slot = Quaternion.Euler(0, 120f + 60f * (int)Def.Kind, 0) * Vector3.forward * 2.5f;
                var want = lead.Pos + Quaternion.LookRotation(lead.Facing) * slot;
                float dl = (lead.Pos - Pos).magnitude;
                if (dl > 12f || (target == null && (want - Pos).magnitude > 1.5f)) aiMove = Util.Flat(want - Pos).normalized * (dl > 6f ? 1f : 0.7f);
            }
            if (aiMove.sqrMagnitude > 1) aiMove.Normalize();
        }
    }
}
