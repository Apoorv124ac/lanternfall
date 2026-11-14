using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>
    /// The Eclipse cult: human servants of the Maw. Every attack is telegraphed on the floor first, so it can be dodged.
    /// Cultist (blade rusher), Gunner (laser-sighted rifle bursts), Brute (ground slam) and the Eclipse Herald boss.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyKind Kind;
        public float Hp, MaxHp, Speed, Dmg, Radius = 0.45f, Height = 1.8f;
        public bool Dead { get; private set; }
        public Vector3 Pos => Util.Flat(transform.position);
        public bool Phase2 { get; private set; }

        public float Burning, Frozen, Stunned, Exposed;
        int chill; float burnTick;
        readonly Dictionary<string, FxPlayer> statusFx = new Dictionary<string, FxPlayer>();

        Humanoid body;
        Pose pose; float poseT, poseDur;
        float attackCd, hitFlash, think, strafe, speedNow;
        bool busy;              // mid-attack: plants its feet
        Telegraph pending;
        Vector3 knock, facing = Vector3.back;
        Hero target;
        int bossStep;
        Coroutine attackRoutine;

        public static Enemy Spawn(EnemyKind kind, Vector3 pos, float hpScale, float dmgScale)
        {
            var go = new GameObject("Enemy " + kind);
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            e.Kind = kind;
            switch (kind)
            {
                case EnemyKind.Cultist: e.MaxHp = 38; e.Speed = 3.6f; e.Dmg = 9; break;
                case EnemyKind.Gunner: e.MaxHp = 28; e.Speed = 3.4f; e.Dmg = 7; break;
                case EnemyKind.Brute: e.MaxHp = 260; e.Speed = 2.7f; e.Dmg = 18; e.Radius = 0.8f; e.Height = 2.6f; break;
                case EnemyKind.Boss: e.MaxHp = 2000; e.Speed = 2.6f; e.Dmg = 20; e.Radius = 1.6f; e.Height = 5.4f; break;
            }
            e.MaxHp *= hpScale; e.Hp = e.MaxHp; e.Dmg *= dmgScale;
            e.body = CharacterArt.BuildEnemy(go.transform, kind);
            Util.FloorDecal(go.transform, Util.Blob(new Color(0, 0, 0, 0.45f), 32), e.Radius + 0.15f, -6);
            e.attackCd = Random.Range(0.6f, 1.6f);
            e.strafe = Random.value * 10f;
            go.transform.rotation = Quaternion.LookRotation(Util.Flat(-pos).sqrMagnitude > 0.01f ? Util.Flat(-pos).normalized : Vector3.back);
            e.facing = go.transform.forward;
            return e;
        }

        void Update()
        {
            var g = Game.I;
            float dt = Time.deltaTime;
            attackCd -= dt; hitFlash -= dt; think -= dt;
            if (pose != Pose.None) { poseT += dt / poseDur; if (poseT >= 1) pose = Pose.None; }
            TickStatuses(dt);
            if (Dead) return;

            bool held = Frozen > 0 || Stunned > 0 || g.State != GameState.Playing;
            if (held && busy && Kind != EnemyKind.Boss) CancelAttack();

            if (think <= 0 || target == null || target.Downed) { think = 0.3f; target = PickTarget(g); }

            Vector3 vel = Vector3.zero;
            if (!held && target != null && !busy)
            {
                var to = Util.Flat(target.Pos - Pos); float d = to.magnitude; var dir = to / Mathf.Max(d, 0.01f);
                facing = dir;
                switch (Kind)
                {
                    case EnemyKind.Cultist:
                        if (d > 1.7f) vel = (dir + Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time + strafe) * 0.4f).normalized * Speed;
                        else if (attackCd <= 0) attackRoutine = StartCoroutine(Swing());
                        break;
                    case EnemyKind.Gunner:
                        if (d < 7f) vel = -dir * Speed * 0.8f;
                        else if (d > 12f) vel = dir * Speed;
                        else vel = Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * 0.8f + strafe) * Speed * 0.5f;
                        if (attackCd <= 0 && d < 16f) attackRoutine = StartCoroutine(Shoot());
                        break;
                    case EnemyKind.Brute:
                        if (d > 2.6f) vel = dir * Speed;
                        else if (attackCd <= 0) attackRoutine = StartCoroutine(Slam(Pos + dir * 1.6f, 2.8f, 1.1f, Dmg));
                        break;
                    case EnemyKind.Boss:
                        if (d > 7f) vel = dir * Speed * (Phase2 ? 1.3f : 1f);
                        if (attackCd <= 0) attackRoutine = StartCoroutine(BossPattern());
                        break;
                }
                if (Burning > 0) vel *= 1.1f;
                if (Exposed > 0) vel *= 0.85f;
            }

            // personal space
            foreach (var o in g.Enemies)
            {
                if (o == this || o.Dead) continue;
                var away = Pos - o.Pos; float min = Radius + o.Radius;
                if (away.sqrMagnitude < min * min && away.sqrMagnitude > 0.0001f) vel += away.normalized * 3f;
            }
            knock = Vector3.MoveTowards(knock, Vector3.zero, 20f * dt);
            transform.position = g.World.Resolve(transform.position + (vel + knock) * dt, Radius);
            speedNow = Util.Flat(vel).magnitude;
            if (!held) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 1f - Mathf.Exp(-(busy ? 4f : 10f) * dt));

            if (Kind == EnemyKind.Gunner) body.SetStance(0f, busy ? 1f : 0.35f, false);
            body.Animate(held ? 0 : dt, held ? 0 : speedNow, pose, poseT, 0);
            if (hitFlash > 0) body.Tint(new Color(1f, 1f, 1f));
            else if (Frozen > 0) body.Tint(new Color(0.65f, 0.9f, 1f));
            else if (Phase2) body.Tint(new Color(1f, 0.6f, 0.75f));
            else body.Tint(Color.clear);
        }

        Hero PickTarget(Game g)
        {
            Hero best = null; float bd = float.MaxValue;
            foreach (var h in g.Heroes)
            {
                if (h.Downed) continue;
                float d = (h.Pos - Pos).sqrMagnitude * (h == g.Leader ? 0.8f : 1f) * (h.Def.Kind == HeroKind.Rime ? 0.7f : 1f); // Rime draws aggro
                if (d < bd) { bd = d; best = h; }
            }
            return best;
        }

        void SetPose(Pose p, float dur) { pose = p; poseT = 0; poseDur = dur; }

        float DmgMult => Game.I.Lantern.LightAt(Pos) < 0.5f ? 1.5f : 1f; // concept rule: enemies outside the light hit harder

        void CancelAttack()
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            if (pending) Destroy(pending.gameObject);
            if (laserNow) Destroy(laserNow.gameObject);
            busy = false; pose = Pose.None;
            attackCd = Mathf.Max(attackCd, 0.6f);
        }

        // ---------------------------------------------------------------- attacks

        IEnumerator Swing()
        {
            busy = true;
            SetPose(Pose.Swing, 1.05f);
            pending = Telegraph.Circle(Pos + facing * 1.15f, 1.25f, 0.62f, Dmg * DmgMult);
            yield return new WaitForSeconds(0.75f);
            busy = false; attackCd = Random.Range(1.4f, 2.2f);
        }

        /// <summary>Gunner attack: a red laser sight tracks the target, locks (blinks), then a 3-round burst. Slide sideways when it locks.</summary>
        IEnumerator Shoot()
        {
            busy = true;
            var laser = Laser(); laserNow = laser;
            float track = 0.75f, lockT = 0.35f, t = 0;
            Vector3 dir = facing;
            while (t < track + lockT)
            {
                if (Dead) { Destroy(laser.gameObject); yield break; }
                t += Time.deltaTime;
                bool locked = t >= track;
                if (!locked && target != null) { dir = Util.Flat(target.Pos - Pos).normalized; facing = dir; }
                var from = MuzzlePos();
                laser.SetPosition(0, from);
                laser.SetPosition(1, from + dir * LaserLength(from, dir));
                float a = locked ? (Mathf.Repeat(t * 16f, 1f) > 0.5f ? 1f : 0.25f) : 0.45f;
                var c = new Color(1f, 0.15f, 0.12f, a);
                laser.startColor = c; laser.endColor = new Color(c.r, c.g, c.b, a * 0.3f);
                laser.widthMultiplier = locked ? 0.07f : 0.035f;
                yield return null;
            }
            Destroy(laser.gameObject);
            for (int i = 0; i < 3; i++)
            {
                if (Dead) yield break;
                SetPose(Pose.Shoot, 0.12f);
                var from = MuzzlePos();
                var spread = Quaternion.Euler(0, Random.Range(-3f, 3f), 0) * dir;
                Projectile.Hostile("shadow-bolt", from, spread, 15f, Dmg * DmgMult, 0.35f);
                Fx.Play("muzzle-smg", from, scale: 0.6f);
                yield return new WaitForSeconds(0.11f);
            }
            yield return new WaitForSeconds(0.2f);
            busy = false; attackCd = Random.Range(2.0f, 2.8f);
        }

        Vector3 MuzzlePos() => body.Muzzle != null ? body.Muzzle.position : Pos + Vector3.up * 1.3f + facing * 0.7f;

        float LaserLength(Vector3 from, Vector3 dir)
        {
            float len = 22f;
            foreach (var pl in Game.I.World.Pillars)
            {
                float t = Gun.RayCircle(from, dir, new Vector3(pl.x, 0, pl.y), pl.z);
                if (t >= 0 && t < len) len = t;
            }
            foreach (var h in Game.I.Heroes)
            {
                if (h.Downed) continue;
                float t = Gun.RayCircle(from, dir, h.Pos, 0.45f);
                if (t >= 0 && t < len) len = t;
            }
            return len;
        }

        static Material laserMat;
        LineRenderer laserNow;
        LineRenderer Laser()
        {
            var go = new GameObject("laser sight");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.useWorldSpace = true;
            if (laserMat == null) laserMat = new Material(Shader.Find("Sprites/Default"));
            lr.material = laserMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        IEnumerator Slam(Vector3 at, float radius, float windup, float dmg)
        {
            busy = true;
            SetPose(Pose.Slam, windup + 0.5f);
            pending = Telegraph.Circle(at, radius, windup, dmg * DmgMult, () =>
            {
                Fx.Play("ground-slam", at, scale: radius / 2.4f);
                Game.I.Shake(0.25f, 0.2f);
            });
            yield return new WaitForSeconds(windup + 0.35f);
            busy = false; attackCd = Random.Range(2.2f, 3f);
        }

        IEnumerator BossPattern()
        {
            var g = Game.I;
            busy = true;
            int step = bossStep++ % (Phase2 ? 5 : 4);
            switch (step)
            {
                case 0: // crushing slam on the target
                case 3:
                {
                    var at = target != null ? target.Pos : Pos + facing * 3f;
                    SetPose(Pose.Slam, 1.7f);
                    pending = Telegraph.Circle(at, Phase2 ? 4.2f : 3.6f, 1.3f, Dmg * 1.1f, () =>
                    {
                        Fx.Play("ground-slam", at, scale: 1.6f);
                        Fx.Play("dark-explosion", at, scale: 0.6f);
                        g.Shake(0.4f, 0.35f);
                    });
                    yield return new WaitForSeconds(1.6f);
                    break;
                }
                case 1: // shadow orb volley
                {
                    SetPose(Pose.Special, 1.2f);
                    yield return new WaitForSeconds(0.6f);
                    int volleys = Phase2 ? 2 : 1, n = Phase2 ? 16 : 11;
                    for (int v = 0; v < volleys; v++)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            float a = (i + v * 0.5f) / n * 360f;
                            Projectile.Hostile("shadow-bolt", Pos + Vector3.up * 2.5f, Quaternion.Euler(0, a, 0) * Vector3.forward, 8.5f, Dmg * 0.5f, 0.7f);
                        }
                        yield return new WaitForSeconds(0.55f);
                    }
                    break;
                }
                case 2: // summon the faithful
                {
                    SetPose(Pose.Ultimate, 1.4f);
                    g.Popup(Pos + Vector3.up * 6f, "RISE, FAITHFUL!", new Color(1f, 0.4f, 0.7f), 22);
                    if (g.Enemies.Count < 6)
                        for (int i = 0; i < (Phase2 ? 4 : 3); i++)
                            g.SpawnEnemy(i == 0 ? EnemyKind.Gunner : EnemyKind.Cultist, Pos + Quaternion.Euler(0, i * 120f + 60f, 0) * Vector3.forward * 4f);
                    yield return new WaitForSeconds(1.3f);
                    break;
                }
                case 4: // eclipse nova: rings that ripple outward (phase 2)
                {
                    SetPose(Pose.Special, 2.2f);
                    for (int i = 0; i < 3; i++)
                    {
                        float r = 3.5f + i * 3f;
                        foreach (var h in g.Heroes)
                        {
                            if (h.Downed) continue;
                            var dir = Util.Flat(h.Pos - Pos); if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                            pending = Telegraph.Circle(Pos + dir.normalized * Mathf.Min(r, dir.magnitude), 2.2f, 0.9f, Dmg * 0.8f, null);
                        }
                        yield return new WaitForSeconds(0.55f);
                    }
                    Fx.Play("dark-explosion", Pos, scale: 1.2f);
                    yield return new WaitForSeconds(0.6f);
                    break;
                }
            }
            busy = false;
            attackCd = Phase2 ? Random.Range(0.8f, 1.3f) : Random.Range(1.4f, 2f);
        }

        // ---------------------------------------------------------------- hits, statuses, Kindred Reactions

        public void Push(Vector3 dir, float force)
        {
            float k = Kind == EnemyKind.Boss ? 0.05f : Kind == EnemyKind.Brute ? 0.3f : 1f;
            knock += Util.Flat(dir).normalized * force * k;
        }

        public void TakeHit(Hit h)
        {
            if (Dead) return;
            var g = Game.I;
            float dmg = h.Damage;
            bool boss = Kind == EnemyKind.Boss;

            if (!h.NoReaction)
            {
                // SHATTER: fire or lightning hits a Frozen target.
                if (Frozen > 0 && (h.Element == Element.Fire || h.Element == Element.Lightning))
                {
                    Frozen = 0; chill = 0;
                    dmg *= 2.5f * g.Party.Reaction;
                    Fx.Play("ice-shatter", Pos, scale: 0.8f);
                    g.Reaction("SHATTER", Pos, new Color(0.6f, 0.95f, 1f));
                    g.HitArea(h.Source, Pos, 2.5f * g.Party.Area, new Hit(h.Source, h.Damage * 0.8f * g.Party.Reaction, Element.Ice, Pos) { NoReaction = true, Push = 5f }, this);
                }
                // OVERLOAD: lightning hits a Burning target.
                else if (Burning > 0 && h.Element == Element.Lightning)
                {
                    Burning = 0;
                    Fx.Play("plasma-explosion", Pos + Vector3.up * 0.8f, scale: 0.8f);
                    g.Reaction("OVERLOAD", Pos, new Color(1f, 0.75f, 0.3f));
                    g.HitArea(h.Source, Pos, 3f * g.Party.Area, new Hit(h.Source, 26f * g.Party.Damage * g.Party.Reaction, Element.None, Pos) { NoReaction = true, StunTime = 1.2f, Push = 7f });
                }
            }
            if (Dead) return;

            if (Exposed > 0) dmg *= 1.25f;
            Hp -= dmg;
            hitFlash = 0.07f;
            if (!busy && !boss && Kind != EnemyKind.Brute) SetPose(Pose.Hit, 0.3f);
            Push(Pos - h.From, h.Push);
            g.DamageNumber(Pos + Vector3.up * (Height + 0.3f), dmg, h.Source);
            if (h.Source) h.Source.OnDealt(dmg, h.Ult);

            float resist = boss ? 0.25f : Kind == EnemyKind.Brute ? 0.6f : 1f;
            switch (h.Element)
            {
                case Element.Fire: Burning = Mathf.Max(Burning, 3f); break;
                case Element.Ice: if (++chill >= (boss ? 6 : 3)) { chill = 0; h.FreezeTime = Mathf.Max(h.FreezeTime, 2f); } break;
                case Element.Holy: Exposed = Mathf.Max(Exposed, 4f); break;
            }
            if (h.FreezeTime > 0) Frozen = Mathf.Max(Frozen, h.FreezeTime * resist);
            if (h.StunTime > 0) Stunned = Mathf.Max(Stunned, h.StunTime * resist);

            if (boss && !Phase2 && Hp < MaxHp * 0.5f)
            {
                Phase2 = true;
                g.Banner("THE HERALD UNVEILS THE ECLIPSE", 2.5f);
                var ecl = Fx.Play("eclipse", Pos + Vector3.up * 6f, scale: 3f, loop: true);
                if (ecl) ecl.transform.SetParent(transform, true);
                g.Shake(0.6f, 0.3f);
            }
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
                    Hp -= 3f * Game.I.Party.Damage;
                    if (Hp <= 0) { Die(); return; }
                }
            }
            Burning -= dt; Frozen -= dt; Stunned -= dt; Exposed -= dt;
            float s = Kind == EnemyKind.Boss ? 2.4f : Kind == EnemyKind.Brute ? 1.5f : 1f;
            Status("burning", Burning > 0, 0.1f, s);
            Status("frozen", Frozen > 0, 0.1f, s * 1.1f);
            Status("stunned", Stunned > 0 && Frozen <= 0, Height + 0.1f, s * 0.8f);
        }

        void Status(string fx, bool on, float y, float scale)
        {
            statusFx.TryGetValue(fx, out var p);
            if (on && p == null)
                statusFx[fx] = Fx.Play(fx, transform.position + Vector3.up * y, follow: transform, scale: scale, loop: true);
            else if (!on && p != null) { p.Stop(); statusFx.Remove(fx); }
        }

        void Die()
        {
            if (Dead) return;
            Dead = true;
            CancelAttack();
            foreach (var p in statusFx.Values) if (p) p.Stop();
            var g = Game.I;
            switch (Kind)
            {
                case EnemyKind.Boss:
                    Fx.Play("dark-explosion", Pos, scale: 2f); g.HitStop(0.25f); g.Shake(0.8f, 0.5f);
                    break;
                case EnemyKind.Brute:
                    Fx.Play("dark-explosion", Pos, scale: 0.9f); g.HitStop(0.1f); g.Shake(0.3f, 0.25f);
                    break;
                default:
                    Fx.Play("shatter-death", Pos + Vector3.up * 0.2f, scale: 0.9f);
                    Fx.Play("poof", Pos, scale: 0.8f);
                    break;
            }
            float xp = Kind == EnemyKind.Cultist ? 4 : Kind == EnemyKind.Gunner ? 5 : Kind == EnemyKind.Brute ? 22 : 0;
            int shards = Kind == EnemyKind.Brute ? 5 : 1;
            for (int i = 0; i < shards; i++) Pickup.Drop(Pos, xp / shards, 0);
            if (Random.value < (Kind == EnemyKind.Brute ? 1f : 0.12f)) Pickup.Drop(Pos, 0, 30);
            g.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
