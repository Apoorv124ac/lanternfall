using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    public enum GameState { Title, Playing, LevelUp, Paused, Defeat, Victory }

    /// <summary>
    /// Lanternfall: a third-person co-op action roguelite. Solo you lead the party and the other three heroes
    /// fight beside you as AI companions; friends can drop in on gamepads and take one over.
    /// Four floors, the party levels up as it goes, and the Eclipse Herald waits at the bottom.
    /// Put this on one GameObject in an empty scene (Lanternfall > Create Game Scene does it) and press Play.
    /// </summary>
    public sealed partial class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState State { get; private set; } = GameState.Title;
        public readonly List<Hero> Heroes = new List<Hero>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public PartyStats Party { get; private set; } = new PartyStats();
        public Lantern Lantern { get; private set; }
        public Arena World { get; private set; }
        public CameraRig CamRig { get; private set; }
        public Hero Leader { get; private set; }
        public Enemy Boss { get; private set; }
        public bool BossAlive => Boss != null && !Boss.Dead;

        int floor, wave, pendingSpawns, kills, reactions, pendingLevels;
        float hitStopT, runTime;
        Light sun;
        Coroutine floorRoutine;
        bool portalOpen;
        Vector3 portalPos = new Vector3(0, 0, 12f);
        FxPlayer portalFx;
        HeroKind titlePick = HeroKind.Cinder;
        List<Upgrade> offer = new List<Upgrade>();
        int offerSel;
        GameState stateBeforePause;

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 45f;
            CamRig = new CameraRig();
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            BuildFloor(0);
            Lantern = Lantern.Create();
            // title-screen lineup
            for (int i = 0; i < HeroDef.Count; i++)
            {
                var h = Hero.Create((HeroKind)i, null, new Vector3((i - 1.5f) * 1.8f, 0, 0));
                h.Facing = Vector3.back; h.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Heroes.Add(h);
            }
            Leader = Heroes[0];
        }

        void BuildFloor(int index)
        {
            floor = index;
            World?.Destroy();
            var def = FloorDef.All[floor];
            World = new Arena(def, transform);
            RenderSettings.ambientLight = def.Ambient;
            RenderSettings.fogColor = def.Fog;
            RenderSettings.fogDensity = def.FogDensity;
            CamRig.Cam.backgroundColor = def.Fog;
            sun.color = def.Sun;
            sun.intensity = 0.55f;
        }

        // ---------------------------------------------------------------- run flow

        void StartRun(Controls p1, HeroKind pick)
        {
            Party = new PartyStats();
            kills = 0; reactions = 0; runTime = 0;
            foreach (var h in Heroes) { h.Input = null; h.FullRestore(); h.UltCharge = 0; }
            Leader = Heroes.Find(h => h.Def.Kind == pick);
            Leader.Input = p1;
            CamRig.Yaw = 0; CamRig.Pitch = 16;
            EnterFloor(0);
        }

        void EnterFloor(int index)
        {
            if (floorRoutine != null) StopCoroutine(floorRoutine);
            ClearEnemies();
            ClosePortal();
            BuildFloor(index);
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                h.transform.position = new Vector3((i - 1.5f) * 1.6f, 0, -12f);
                h.Facing = Vector3.forward; h.transform.rotation = Quaternion.identity;
                h.FullRestore();
            }
            Lantern.transform.position = Leader.Pos + Vector3.up * 2.3f;
            CamRig.Yaw = 0;
            State = GameState.Playing;
            floorRoutine = StartCoroutine(RunFloor());
        }

        IEnumerator RunFloor()
        {
            var def = FloorDef.All[floor];
            Banner(def.Name.ToUpper(), 3f, def.Subtitle);
            yield return new WaitForSeconds(2.5f);
            int humans = Heroes.FindAll(h => !h.IsAI).Count;
            float hp = (1f + 0.45f * floor) * (1f + 0.15f * (humans - 1)), dmg = 1f + 0.15f * floor;

            for (wave = 0; wave < def.Waves.Length; wave++)
            {
                bool bossWave = def.Boss && wave == def.Waves.Length - 1;
                if (bossWave)
                {
                    Banner("THE ECLIPSE HERALD", 3f, "Servant of the Maw that ate the sun");
                    var at = new Vector3(0, 0, 10f);
                    Fx.Play("summon-rise", at, scale: 2.5f);
                    Fx.Play("eclipse", at + Vector3.up * 7f, scale: 3f);
                    Shake(1f, 0.2f);
                    yield return new WaitForSeconds(1.6f);
                    Boss = Enemy.Spawn(EnemyKind.Boss, at, hp, dmg);
                    Enemies.Add(Boss);
                }
                else
                {
                    Banner($"WAVE {wave + 1} / {(def.Boss ? def.Waves.Length - 1 : def.Waves.Length)}", 1.6f);
                    yield return new WaitForSeconds(1.2f);
                    foreach (var group in def.Waves[wave])
                        for (int i = 0; i < group.count; i++)
                        {
                            SpawnEnemy(group.kind, World.RandomEdge(Leader.Pos), hp, dmg);
                            yield return new WaitForSeconds(0.3f);
                        }
                }
                while (pendingSpawns > 0 || Enemies.Count > 0) yield return null;
                if (bossWave) break;
                yield return new WaitForSeconds(1.2f);
            }

            if (def.Boss) { Victory(); yield break; }

            Banner("FLOOR CLEARED", 2.5f, "The ember grows. Step into the light to descend.");
            foreach (var h in Heroes) { if (h.Downed) h.Revive(1f); else h.Heal(h.MaxHp); }
            Fx.Play("heal-rose", Leader.Pos, scale: 1.2f);
            OpenPortal();
        }

        void OpenPortal()
        {
            portalOpen = true;
            portalFx = Fx.Play("spawn-portal", portalPos, scale: 1.6f, loop: true);
        }

        void ClosePortal()
        {
            portalOpen = false;
            if (portalFx) portalFx.Stop();
        }

        public void SpawnEnemy(EnemyKind kind, Vector3 at, float hpScale = -1, float dmgScale = -1)
        {
            if (hpScale < 0) { hpScale = 1f + 0.45f * floor; dmgScale = 1f + 0.15f * floor; }
            StartCoroutine(SpawnOne(kind, World.Resolve(at, 0.6f), hpScale, dmgScale));
        }

        IEnumerator SpawnOne(EnemyKind kind, Vector3 at, float hp, float dmg)
        {
            pendingSpawns++;
            Fx.Play("spawn-portal", at, scale: kind == EnemyKind.Brute ? 1f : 0.7f);
            yield return new WaitForSeconds(0.8f);
            if (State == GameState.Playing || State == GameState.LevelUp) Enemies.Add(Enemy.Spawn(kind, at, hp, dmg));
            pendingSpawns--;
        }

        void ClearEnemies()
        {
            StopAllCoroutines();
            floorRoutine = null;
            foreach (var e in Enemies) if (e) Destroy(e.gameObject);
            Enemies.Clear();
            foreach (var t in Telegraph.Active.ToArray()) if (t) Destroy(t.gameObject);
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None)) Destroy(p.gameObject);
            pendingSpawns = 0;
            Boss = null;
        }

        void Victory()
        {
            State = GameState.Victory;
            RenderSettings.ambientLight = Util.Hex("#8a6a50");
            RenderSettings.fogColor = Util.Hex("#f2b07a");
            RenderSettings.fogDensity = 0.012f;
            CamRig.Cam.backgroundColor = Util.Hex("#f2b07a");
            sun.color = Util.Hex("#ffd9a0"); sun.intensity = 1.2f;
            Fx.Play("sunburst", Leader.Pos + Vector3.up * 3f, scale: 4f);
            Banner("THE SUN RISES AGAIN", 999f);
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            hitStopT -= udt;
            bool paused = State == GameState.LevelUp || State == GameState.Paused;
            Time.timeScale = paused ? 0f : hitStopT > 0 ? 0.05f : 1f;

            switch (State)
            {
                case GameState.Title: TitleUpdate(udt); break;
                case GameState.Playing: PlayingUpdate(); break;
                case GameState.LevelUp: LevelUpUpdate(); break;
                case GameState.Paused:
                    if (Controls.PausePressed()) State = stateBeforePause;
                    break;
                case GameState.Defeat:
                    if (Leader.Input.Confirm) { State = GameState.Playing; EnterFloor(floor); }
                    break;
                case GameState.Victory:
                    if (Leader.Input.Confirm) ToTitle();
                    break;
            }

            // cursor: locked while the mouse player is fighting
            bool wantLock = State == GameState.Playing && Leader != null && Leader.Input != null && Leader.Input.UsesMouse;
            Cursor.lockState = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !wantLock;

            TickPops(udt);
        }

        void LateUpdate()
        {
            float udt = Time.unscaledDeltaTime;
            if (State == GameState.Title) CamRig.Orbit(new Vector3(0, 0, 0), 7.5f, 1.3f, udt);
            else CamRig.Follow(Leader, BossAlive, udt);
        }

        void TitleUpdate(float udt)
        {
            runTime += udt;
            // whoever touches a device picks a hero and becomes P1
            Controls who = null;
            foreach (var pad in Gamepad.all)
            {
                var c = new GamepadControls(pad);
                int nx = PadNav(pad);
                if (nx != 0) titlePick = (HeroKind)(((int)titlePick + nx + HeroDef.Count) % HeroDef.Count);
                if (c.AnyJoin) who = c;
            }
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) titlePick = (HeroKind)(((int)titlePick + 3) % 4);
                if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) titlePick = (HeroKind)(((int)titlePick + 1) % 4);
                if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) who = new KeyboardControls(0);
            }
            if (titleClick >= 0) { titlePick = (HeroKind)titleClick; titleClick = -1; who = new KeyboardControls(0); }

            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                bool sel = (int)h.Def.Kind == (int)titlePick;
                var want = new Vector3((i - 1.5f) * 1.8f, 0, sel ? -0.8f : 0);
                h.transform.position = Vector3.Lerp(h.transform.position, want, 1f - Mathf.Exp(-8f * udt));
                var toCam = Util.Flat(CamRig.Cam.transform.position - h.Pos).normalized;
                h.transform.rotation = Quaternion.LookRotation(toCam);
            }
            if (who != null) StartRun(who, titlePick);
        }

        readonly Dictionary<Gamepad, float> padNav = new Dictionary<Gamepad, float>();
        int PadNav(Gamepad pad)
        {
            float x = pad.leftStick.ReadValue().x + pad.dpad.ReadValue().x;
            padNav.TryGetValue(pad, out float last);
            padNav[pad] = x;
            if (x > 0.6f && last <= 0.6f) return 1;
            if (x < -0.6f && last >= -0.6f) return -1;
            return 0;
        }

        void PlayingUpdate()
        {
            runTime += Time.deltaTime;
            if (Controls.PausePressed()) { stateBeforePause = State; State = GameState.Paused; return; }
            PollDropIn();

            if (pendingLevels > 0) { OpenLevelUp(); return; }

            if (portalOpen && (Leader.Pos - portalPos).magnitude < 2.2f && floor + 1 < FloorDef.All.Length)
            {
                Fx.Play("blink-out", Leader.Pos, scale: 1.2f);
                EnterFloor(floor + 1);
                return;
            }

            if (Heroes.TrueForAll(h => h.Downed))
            {
                State = GameState.Defeat;
                Banner("THE EMBER GUTTERS", 999f, "Press Enter / A to try this floor again. Your levels are kept.");
            }
        }

        void PollDropIn()
        {
            foreach (var pad in Gamepad.all)
            {
                if (Heroes.Exists(h => h.Input is GamepadControls gc && gc.Pad == pad)) continue;
                var c = new GamepadControls(pad);
                if (!c.AnyJoin) continue;
                var ai = Heroes.Find(h => h.IsAI);
                if (ai == null) return;
                ai.Input = c;
                Popup(ai.Pos + Vector3.up * 2.4f, $"Player joined as {ai.Def.Name}", ai.Def.Accent, 18);
                return;
            }
            var k = Keyboard.current;
            if (k != null && !Heroes.Exists(h => h.Input is KeyboardControls kc && kc.Layout == 1) && (k.commaKey.wasPressedThisFrame || k.slashKey.wasPressedThisFrame))
            {
                var ai = Heroes.Find(h => h.IsAI);
                if (ai != null) { ai.Input = new KeyboardControls(1); Popup(ai.Pos + Vector3.up * 2.4f, $"Player joined as {ai.Def.Name}", ai.Def.Accent, 18); }
            }
        }

        void ToTitle()
        {
            ClearEnemies();
            ClosePortal();
            BuildFloor(0);
            State = GameState.Title;
            banner = null;
            for (int i = 0; i < Heroes.Count; i++) { Heroes[i].Input = null; Heroes[i].FullRestore(); Heroes[i].transform.position = new Vector3((i - 1.5f) * 1.8f, 0, 0); }
        }

        // ---------------------------------------------------------------- progression

        public void GainXp(float xp)
        {
            int gained = Party.AddXp(xp);
            if (gained > 0)
            {
                pendingLevels += gained;
                Fx.Play("level-up", Leader.Pos, scale: 1.1f);
                foreach (var h in Heroes) { if (h.Downed) h.Revive(0.5f); h.Heal(h.MaxHp * 0.3f, false); }
            }
        }

        void OpenLevelUp()
        {
            offer = Upgrade.Roll(Party, 3);
            if (offer.Count == 0) { pendingLevels = 0; return; }
            offerSel = 1 % offer.Count;
            State = GameState.LevelUp;
        }

        void LevelUpUpdate()
        {
            var input = Leader.Input;
            int nx = input.NavX();
            if (nx != 0) offerSel = (offerSel + nx + offer.Count) % offer.Count;
            if (offerClick >= 0) { offerSel = offerClick; offerClick = -1; Choose(); return; }
            if (input.Confirm || (input is KeyboardControls kc && kc.Layout == 0 && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)) Choose();
        }

        void Choose()
        {
            var u = offer[offerSel];
            u.Take(Party);
            if (u.Id == "heart") foreach (var h in Heroes) h.Heal(h.MaxHp, false);
            Popup(Leader.Pos + Vector3.up * 2.6f, u.Name.ToUpper(), u.Color, 24);
            pendingLevels--;
            State = GameState.Playing;
        }

        // ---------------------------------------------------------------- queries & combat helpers

        /// <summary>Nearest living enemy in range. With a direction, enemies in front are preferred and those behind (dot below minDot) skipped.</summary>
        public Enemy NearestEnemy(Vector3 from, float range, Vector3 dir, List<Enemy> exclude = null, float minDot = -1f)
        {
            Enemy best = null; float bestScore = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Dead || (exclude != null && exclude.Contains(e))) continue;
                var to = Util.Flat(e.Pos - from); float d = to.magnitude;
                if (d > range) continue;
                float dot = dir == Vector3.zero || d < 0.01f ? 1f : Vector3.Dot(to / d, dir.normalized);
                if (dot < minDot) continue;
                float score = d * (1.6f - 0.6f * dot);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        public Enemy RandomEnemy(Vector3 from, float range)
        {
            var list = Enemies.FindAll(e => !e.Dead && (e.Pos - from).sqrMagnitude < range * range);
            return list.Count == 0 ? null : list[Random.Range(0, list.Count)];
        }

        public int CountEnemies(Vector3 at, float r)
        {
            int n = 0;
            foreach (var e in Enemies) if (!e.Dead && (e.Pos - at).sqrMagnitude < (r + e.Radius) * (r + e.Radius)) n++;
            return n;
        }

        public void HitArea(Hero src, Vector3 at, float radius, Hit hit, Enemy exclude = null)
        {
            foreach (var e in Enemies.ToArray())
            {
                if (e == null || e.Dead || e == exclude) continue;
                float r = radius + e.Radius;
                if ((e.Pos - at).sqrMagnitude <= r * r) e.TakeHit(hit);
            }
        }

        public void OnEnemyDied(Enemy e)
        {
            Enemies.Remove(e);
            kills++;
        }

        public void HitStop(float t) => hitStopT = Mathf.Max(hitStopT, t);
        public void Shake(float t, float amp) => CamRig.Shake(t, amp);

        public void Reaction(string name, Vector3 at, Color c)
        {
            reactions++;
            HitStop(0.08f);
            Shake(0.25f, 0.2f);
            Popup(at + Vector3.up * 2.6f, name + "!", c, 30);
        }
    }
}
