using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    public enum Loco { Walk, Run, Sprint, Crouch, Slide }

    /// <summary>
    /// A Lanternbearer: an armoured gunner with an elemental firearm, a special and an ultimate.
    /// Twin-stick controls with the reel's movement set (walk, run, tactical sprint, crouch, slide with fire, stamina).
    /// Driven by a human (Controls) or by the companion AI when Input is null.
    /// </summary>
    public sealed class Hero : MonoBehaviour
    {
        public HeroDef Def;
        public Controls Input;           // null = AI companion
        public Gun Gun;
        public float Hp, UltCharge, Stamina = 100f;
        public bool Downed, Invulnerable;
        public float ReviveProgress, DownedTime;
        public float SpecialCd;
        public Vector3 Facing = Vector3.forward, AimPoint;
        public float Radius = 0.45f;
        public Loco Loco { get; private set; }
        public bool Ads { get; private set; }
        public float SpeedNow { get; private set; }
        public Humanoid Body { get; private set; }

        public bool IsAI => Input == null;
        public float MaxHp => Def.MaxHp + Game.I.Party.MaxHpBonus;
        public float MaxStamina => 100f * Game.I.Party.StaminaMax;
        public Vector3 Pos => Util.Flat(transform.position);
        public float SpecialReady => 1f - Mathf.Clamp01(SpecialCd / (Def.SpecialCooldown * Game.I.Party.Cooldown));

        PartyStats P => Game.I.Party;
        Pose pose; float poseT, poseDur;
        float hitFlash, slideT, staminaDelay, fireHold;
        bool crouchToggle;
        Vector3 slideDir, knock, vel;
        float aiThink, aiStrafe, aiBurst;
        Vector3 aiMove, aiAim;
        bool aiFire, aiSpecial, aiUlt, aiSlide, aiRevive, aiRun;

        const float ReviveTime = 1.4f, ReviveReach = 2f, SelfReviveTime = 10f, SlideTime = 0.62f, SlideSpeed = 12.5f;

        public static Hero Create(HeroKind kind, Controls input, Vector3 pos)
        {
            var go = new GameObject("Hero " + kind);
            go.transform.position = pos;
            var h = go.AddComponent<Hero>();
            h.Def = HeroDef.Get(kind);
            h.Input = input;
            h.Gun = new Gun(h.Def.Weapon);
            h.Body = CharacterArt.BuildHero(go.transform, h.Def);
            h.Hp = h.Def.MaxHp;
            h.aiStrafe = Random.value * 10f;
            Util.FloorDecal(go.transform, Util.Blob(new Color(0, 0, 0, 0.45f), 32), 0.55f, -6);
            // a ring under each hero in their colour, so you can read the squad from above
            Util.MeshObj("Ring", Util.Ring(0.55f, 0.63f, 32), go.transform, new Vector3(0, 0.025f, 0), Util.Glow(h.Def.Accent));
            return h;
        }

        void Update()
        {
            var g = Game.I;
            float dt = Time.deltaTime;
            SpecialCd -= dt; hitFlash -= dt;
            Gun.Tick(dt);
            if (pose != Pose.None) { poseT += dt / poseDur; if (poseT >= 1) pose = Pose.None; }

            if (g.State != GameState.Playing) { Animate(dt); return; }

            if (Downed)
            {
                DownedTime += dt;
                ReviveProgress = Mathf.Max(0, ReviveProgress - dt * 0.2f);
                if (DownedTime > SelfReviveTime) Revive(0.3f);
                Animate(dt);
                return;
            }

            if (g.Lantern.LightAt(Pos) > 0.5f) Heal(P.LightRegen * dt, false);

            // ---- intent
            Vector3 move, aim; bool fire, special, ult, slide, revive, run, sprintTap = false, crouchPress = false, reload = false;
            if (IsAI)
            {
                Think(dt);
                move = aiMove; aim = aiAim; fire = aiFire; special = aiSpecial; ult = aiUlt; slide = aiSlide; revive = aiRevive; run = aiRun;
                Ads = false;
            }
            else
            {
                var m = Input.Move;
                var yaw = Quaternion.Euler(0, g.CamRig.Yaw, 0);
                move = yaw * new Vector3(m.x, 0, m.y);
                aim = ResolveAim(yaw);
                fire = Input.Fire; special = Input.Special; ult = Input.Ultimate; revive = Input.Interact;
                run = Input.Run; sprintTap = Input.SprintTap(); crouchPress = Input.Crouch; reload = Input.Reload;
                Ads = Input.AimDownSights;
                slide = false;
                if (crouchPress && (Loco == Loco.Run || Loco == Loco.Sprint) && move.sqrMagnitude > 0.1f) { slide = true; crouchPress = false; }
            }

            // ---- locomotion state machine (walk / run / tactical sprint / crouch / slide)
            bool moving = move.sqrMagnitude > 0.01f;
            if (crouchPress) crouchToggle = !crouchToggle;
            if (slideT > 0)
            {
                slideT -= dt;
                if (slideT <= 0) { Loco = crouchToggle ? Loco.Crouch : Loco.Walk; Invulnerable = false; }
            }
            else if (slide && Stamina >= 15f && moving)
            {
                slideT = SlideTime; slideDir = move.normalized; Loco = Loco.Slide;
                Stamina -= 15f; staminaDelay = 0.7f;
                Invulnerable = true;
                Fx.Play("poof", Pos, scale: 0.35f);
                crouchToggle = false;
            }
            else
            {
                if (sprintTap && moving && Stamina > 10f) Loco = Loco.Sprint;
                else if (Loco == Loco.Sprint && (!moving || Stamina <= 0 || (!run && !IsAI) || fire || Ads)) Loco = run && moving ? Loco.Run : Loco.Walk;
                else if (Loco != Loco.Sprint) Loco = crouchToggle ? Loco.Crouch : run && moving ? Loco.Run : Loco.Walk;
                if (IsAI && aiRun && moving && Stamina > 30f) Loco = Loco.Sprint;
                if (Loco == Loco.Run || Loco == Loco.Sprint) crouchToggle = false;
            }
            if (slideT > 0 && slideT < SlideTime - 0.35f) Invulnerable = false; // i-frames only at the start of the slide

            // stamina
            if (Loco == Loco.Sprint) { Stamina -= 24f * dt; staminaDelay = 0.6f; }
            else if ((staminaDelay -= dt) <= 0) Stamina = Mathf.Min(MaxStamina, Stamina + 32f * dt);
            Stamina = Mathf.Max(0, Stamina);

            float speed = Def.Speed * P.MoveSpeed;
            switch (Loco)
            {
                case Loco.Walk: speed *= 0.8f; break;
                case Loco.Run: speed *= 1.08f; break;
                case Loco.Sprint: speed *= 1.5f; break;
                case Loco.Crouch: speed *= 0.45f; break;
            }
            if (Ads) speed *= 0.6f;
            if (fire && Loco != Loco.Slide) speed *= 0.85f;

            Vector3 want = slideT > 0 ? slideDir * Mathf.Lerp(Def.Speed * 0.6f, SlideSpeed, slideT / SlideTime) : move * speed;
            vel = slideT > 0 ? want : Vector3.MoveTowards(vel, want, (moving ? 45f : 60f) * dt); // snappy accel
            knock = Vector3.MoveTowards(knock, Vector3.zero, 20f * dt);
            var p = transform.position + (vel + knock) * dt;
            foreach (var e in g.Enemies)
            {
                var d = Util.Flat(p - e.Pos); float min = Radius + e.Radius;
                if (d.sqrMagnitude < min * min && d.sqrMagnitude > 0.0001f) p += d.normalized * (min - d.magnitude) * 0.5f;
            }
            transform.position = g.World.Resolve(p, Radius);
            SpeedNow = Util.Flat(vel).magnitude;

            // ---- facing: always toward the aim, except while sprinting
            if (Loco == Loco.Sprint && moving) Facing = move.normalized;
            else if (aim.sqrMagnitude > 0.01f) Facing = aim.normalized;
            else if (moving) Facing = move.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Facing), 1f - Mathf.Exp(-20f * dt));

            // ---- revive
            bool reviving = false;
            if (revive)
                foreach (var h in g.Heroes)
                    if (h != this && h.Downed && (h.Pos - Pos).sqrMagnitude < ReviveReach * ReviveReach)
                    {
                        reviving = true;
                        h.ReviveProgress += dt / ReviveTime;
                        if (h.ReviveProgress >= 1) h.Revive(0.5f);
                        break;
                    }

            // ---- weapons and powers
            if (!reviving)
            {
                if (reload && Gun.StartReload()) SetPose(Pose.Reload, Gun.Def.ReloadTime * P.ReloadTime);
                if (ult && UltCharge >= 100) { UltCharge = 0; StartCoroutine(Ultimate(Facing)); }
                else if (special && SpecialCd <= 0) { SpecialCd = Def.SpecialCooldown * P.Cooldown; Special(Facing); }
                else if (fire && Loco != Loco.Sprint)
                {
                    float spreadMult = Loco == Loco.Crouch ? 0.25f : Loco == Loco.Run || Loco == Loco.Slide ? 1.3f : 0.75f;
                    if (Ads) spreadMult = 0f;
                    var muzzle = Body.Muzzle ? Body.Muzzle.position : Pos + Vector3.up * 1.3f + Facing * 0.8f;
                    bool wasReloading = Gun.Reloading;
                    if (Gun.TryFire(this, muzzle, Facing, spreadMult)) SetPose(Pose.Shoot, 0.08f);
                    else if (!wasReloading && Gun.Reloading) SetPose(Pose.Reload, Gun.Def.ReloadTime * P.ReloadTime);
                    fireHold = 0.3f;
                }
            }
            fireHold -= dt;

            Animate(dt);
        }

        void Animate(float dt)
        {
            float aimAmt = Loco == Loco.Sprint ? 0f : (fireHold > 0 || Ads) ? 1f : 0.55f;
            Body.SetStance(Loco == Loco.Crouch ? 1f : 0f, aimAmt, Loco == Loco.Slide);
            Body.Animate(dt, Loco == Loco.Slide ? 0 : SpeedNow, pose, poseT, Downed ? 1 : 0);
            if (hitFlash > 0) Body.Tint(new Color(1f, 0.55f, 0.55f));
            else if (Invulnerable) Body.Tint(new Color(0.85f, 0.92f, 1f));
            else Body.Tint(Color.clear);
        }

        void SetPose(Pose p, float dur) { pose = p; poseT = 0; poseDur = dur; }

        /// <summary>Mouse: the point under the cursor (snapped to an enemy near it). Stick: the stick direction with light aim assist.</summary>
        Vector3 ResolveAim(Quaternion camYaw)
        {
            var g = Game.I;
            if (Input.UsesMouse && g.Leader == this)
            {
                AimPoint = g.CamRig.MouseGround(1.1f);
                Enemy best = null; float bd = 1.4f;
                foreach (var e in g.Enemies)
                {
                    if (e.Dead) continue;
                    float d = (Util.Flat(AimPoint) - e.Pos).magnitude - e.Radius;
                    if (d < bd) { bd = d; best = e; }
                }
                var target = best != null ? best.Pos : Util.Flat(AimPoint);
                var dir = Util.Flat(target - Pos);
                return dir.sqrMagnitude > 0.04f ? dir.normalized : Facing;
            }
            var st = Input.AimStick;
            Vector3 baseDir = st.sqrMagnitude > 0.01f ? camYaw * new Vector3(st.x, 0, st.y) : (Input.Move.sqrMagnitude > 0.01f ? camYaw * new Vector3(Input.Move.x, 0, Input.Move.y) : Facing);
            var aimDir = SoftAim(baseDir.normalized, Gun.Def.Range, st.sqrMagnitude > 0.01f ? 14f : 35f);
            AimPoint = Pos + aimDir * 6f;
            return aimDir;
        }

        // ---------------------------------------------------------------- damage / healing

        public void Damage(float amount, Vector3 from)
        {
            if (Downed || Invulnerable || Game.I.State != GameState.Playing) return;
            if (IsAI) amount *= 0.75f;
            if (Loco == Loco.Crouch) amount *= 0.85f;
            Hp -= amount;
            hitFlash = 0.12f;
            knock += Util.Flat(Pos - from).normalized * 4f;
            if (Game.I.Leader == this) { Game.I.Shake(0.15f, 0.15f); Game.I.HurtFlash(); }
            if (Hp <= 0)
            {
                Hp = 0; Downed = true; ReviveProgress = 0; DownedTime = 0; Invulnerable = false; slideT = 0; Loco = Loco.Walk;
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

        public void OnDealt(float dmg, bool ult)
        {
            if (!ult) UltCharge = Mathf.Min(100, UltCharge + dmg * 0.22f * P.UltRate);
            if (P.Lifesteal > 0) Heal(dmg * P.Lifesteal, false);
        }

        public void FullRestore()
        {
            Downed = false; Hp = MaxHp; ReviveProgress = 0; SpecialCd = 0; Invulnerable = false; slideT = 0; Stamina = MaxStamina;
            Loco = Loco.Walk; crouchToggle = false; Gun.Refill();
        }

        Vector3 Hand => Body.Muzzle ? Body.Muzzle.position : Pos + Vector3.up * 1.3f + Facing * 0.6f;
        Vector3 Chest => Pos + Vector3.up * 1.25f;

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
                float score = ang * 0.15f + d;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best == null ? dir : Util.Flat(best.Pos - Pos).normalized;
        }

        float Dmg(float baseDmg) => baseDmg * P.Damage;

        // ---------------------------------------------------------------- powers (special + ultimate)

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
            aiThink -= dt; aiBurst -= dt;
            aiSlide = false;
            var lead = g.Leader;

            // 1. get out of telegraphed attacks (slide if it's close)
            foreach (var tg in Telegraph.Active)
            {
                var d = Pos - tg.Pos; d.y = 0;
                if (d.magnitude < tg.Radius + 0.7f)
                {
                    aiMove = d.sqrMagnitude < 0.01f ? transform.right : d.normalized;
                    aiSlide = Stamina > 40f && Random.value < 0.08f;
                    aiRun = true; aiRevive = false;
                    return;
                }
            }

            if (aiThink > 0) return;
            aiThink = 0.12f;
            aiFire = aiSpecial = aiUlt = aiRevive = aiRun = false;
            aiMove = Vector3.zero;

            // 2. revive a downed ally
            Hero down = null; float dd = float.MaxValue;
            foreach (var h in g.Heroes)
                if (h != this && h.Downed) { float d = (h.Pos - Pos).sqrMagnitude; if (d < dd) { dd = d; down = h; } }
            if (down != null && Hp > MaxHp * 0.3f)
            {
                if (Def.Kind == HeroKind.Sol && SpecialCd <= 0 && dd < 30f) { aiSpecial = true; return; }
                if (dd > 1.5f * 1.5f) { aiMove = Util.Flat(down.Pos - Pos).normalized; aiRun = dd > 36f; }
                else aiRevive = true;
                return;
            }

            // 3. fight: hold preferred range, strafe, shoot in bursts
            var target = g.NearestEnemy(lead != null && !lead.Downed ? lead.Pos : Pos, 18f, Vector3.zero);
            if (target != null && (target.Pos - Pos).magnitude < 22f)
            {
                var to = Util.Flat(target.Pos - Pos); float d = to.magnitude; var dir = to / Mathf.Max(d, 0.01f);
                float range = Def.PreferredRange;
                var side = Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * 0.7f + aiStrafe);
                if (d > range + 1.5f) aiMove = dir + side * 0.3f;
                else if (d < range - 2.5f) aiMove = -dir + side * 0.5f;
                else aiMove = side * 0.7f;
                // aim with a little human wobble
                aiAim = Quaternion.Euler(0, Mathf.Sin(Time.time * 3f + aiStrafe) * 4f, 0) * dir;
                if (aiBurst < -0.25f) aiBurst = Random.Range(0.6f, 1.4f);
                aiFire = d < Gun.Def.Range * 0.95f && aiBurst > 0;
                if (Gun.Ammo == 0 && !Gun.Reloading) Gun.StartReload();

                int near = g.CountEnemies(Pos, 4.5f * P.Area);
                switch (Def.Kind)
                {
                    case HeroKind.Cinder: aiSpecial = near >= 2; break;
                    case HeroKind.Rime: aiSpecial = near >= 2 || (near >= 1 && Hp < MaxHp * 0.5f); break;
                    case HeroKind.Volt: aiSpecial = g.CountEnemies(target.Pos, 6f) >= 2 || target.Kind != EnemyKind.Cultist; break;
                    case HeroKind.Sol: aiSpecial = g.Heroes.Exists(h => !h.Downed && h.Hp < h.MaxHp * 0.55f && (h.Pos - Pos).sqrMagnitude < 36f); break;
                }
                aiUlt = UltCharge >= 100 && (g.CountEnemies(Pos, 14f) >= 4 || g.BossAlive || (Def.Kind == HeroKind.Sol && g.Heroes.FindAll(h => h.Downed || h.Hp < h.MaxHp * 0.4f).Count >= 2));
            }

            // 4. stay with the leader
            if (lead != null && lead != this)
            {
                var slot = Quaternion.Euler(0, 120f + 60f * (int)Def.Kind, 0) * Vector3.forward * 2.8f;
                var want = lead.Pos + Quaternion.LookRotation(lead.Facing) * slot;
                float dl = (lead.Pos - Pos).magnitude;
                if (dl > 12f || (target == null && (want - Pos).magnitude > 1.5f)) { aiMove = Util.Flat(want - Pos).normalized; aiRun = dl > 7f; }
                if (target == null) aiAim = lead.Facing;
            }
            if (aiMove.sqrMagnitude > 1) aiMove.Normalize();
        }
    }
}
