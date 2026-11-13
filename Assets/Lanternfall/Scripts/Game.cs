using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    public enum GameState { Lobby, Playing, Cleared, Defeat }

    /// <summary>
    /// Floor 1 vertical slice. Drop this on an empty GameObject in an empty scene (or use the
    /// Lanternfall > Create Slice Scene menu) and press Play. Builds camera, lights, arena, Lantern and HUD itself.
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState State { get; private set; } = GameState.Lobby;
        public readonly List<Hero> Heroes = new List<Hero>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public Lantern Lantern { get; private set; }
        public Arena Arena { get; private set; }

        Camera cam;
        float camDist = 18f, shakeT, shakeAmp, hitStopT;
        Vector3 camFocus;
        int wave, pendingSpawns, kills, reactions;
        string banner; float bannerT;
        const int MaxPlayers = 4;
        static readonly Color Ember = new Color(1f, 0.68f, 0.21f);

        struct Pop { public Vector3 At; public string Text; public Color Color; public float T, Size; }
        readonly List<Pop> pops = new List<Pop>();

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.03f, 0.06f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.rotation = Quaternion.Euler(52f, 0, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.11f, 0.2f);
            var moon = new GameObject("Eclipse Light").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.45f, 0.42f, 0.75f);
            moon.intensity = 0.25f;
            moon.transform.rotation = Quaternion.Euler(60, -30, 0);

            Arena = new Arena(transform);
            Lantern = Lantern.Create(Vector3.zero);
            camFocus = Vector3.zero;
            PlaceCamera(0f);
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            hitStopT -= udt;
            Time.timeScale = hitStopT > 0 ? 0.05f : 1f;

            PollJoins();

            switch (State)
            {
                case GameState.Lobby:
                    if (Heroes.Count > 0 && Controls.StartPressed()) StartCoroutine(RunFloor());
                    break;
                case GameState.Playing:
                    if (Heroes.Count > 0 && Heroes.TrueForAll(h => h.Downed))
                    {
                        State = GameState.Defeat;
                        Banner("THE EMBER GUTTERS", 999f);
                        StopAllCoroutines();
                    }
                    break;
                case GameState.Cleared:
                case GameState.Defeat:
                    if (Controls.StartPressed()) ResetRun();
                    break;
            }

            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i]; p.T += udt; p.At += Vector3.up * udt * 0.8f; pops[i] = p;
                if (p.T > 1.1f) pops.RemoveAt(i);
            }
            bannerT -= udt;
        }

        void LateUpdate() => PlaceCamera(Time.unscaledDeltaTime);

        void PlaceCamera(float dt)
        {
            Vector3 sum = Lantern ? Lantern.Pos : Vector3.zero; int n = 1;
            float minX = sum.x, maxX = sum.x, minZ = sum.z, maxZ = sum.z;
            foreach (var h in Heroes)
            {
                if (h.Downed) continue;
                sum += h.Pos; n++;
                minX = Mathf.Min(minX, h.Pos.x); maxX = Mathf.Max(maxX, h.Pos.x);
                minZ = Mathf.Min(minZ, h.Pos.z); maxZ = Mathf.Max(maxZ, h.Pos.z);
            }
            var target = sum / n;
            target.x = Mathf.Clamp(target.x, -5f, 5f);
            target.z = Mathf.Clamp(target.z, -3f, 2f);
            float spread = Mathf.Max(maxX - minX, (maxZ - minZ) * 1.6f);
            float wantDist = Mathf.Clamp(17f + spread * 0.55f, 17f, 27f);
            float k = dt <= 0 ? 1 : 1f - Mathf.Exp(-4f * dt);
            camFocus = Vector3.Lerp(camFocus, target, k);
            camDist = Mathf.Lerp(camDist, wantDist, k);

            var rot = cam.transform.rotation;
            var pos = camFocus + rot * new Vector3(0, 0, -camDist);
            if (shakeT > 0)
            {
                shakeT -= dt;
                pos += (Vector3)(Random.insideUnitCircle * shakeAmp);
            }
            cam.transform.position = pos;
        }

        // ---------------------------------------------------------------- players

        void PollJoins()
        {
            if (State != GameState.Lobby && State != GameState.Playing) return;
            if (Heroes.Count >= MaxPlayers) return;

            foreach (var pad in Gamepad.all)
            {
                if (Heroes.Exists(h => h.Input is GamepadControls gc && gc.Pad == pad)) continue;
                var c = new GamepadControls(pad);
                if (c.AnyJoin) { Join(c); return; }
            }
            for (int layout = 0; layout < 2; layout++)
            {
                if (Heroes.Exists(h => h.Input is KeyboardControls kc && kc.Layout == layout)) continue;
                var c = new KeyboardControls(layout);
                if (c.AnyJoin) { Join(c); return; }
            }
        }

        void Join(Controls c)
        {
            int slot = 0;
            while (Heroes.Exists(h => h.Slot == slot)) slot++;
            var kind = FreeKind(HeroKind.Cinder);
            var spawn = Lantern.Pos + Quaternion.Euler(0, slot * 90f + 45f, 0) * Vector3.forward * 2f;
            var hero = Hero.Create(kind, c, slot, Arena.Resolve(spawn, 0.5f));
            hero.AttackCd = 0.3f; // the join press shouldn't also fire
            Heroes.Add(hero);
            Fx.Play("spawn-portal", hero.Pos, scale: 0.5f);
            Popup(hero.Pos + Vector3.up * 2.4f, $"P{slot + 1} {hero.Def.Name}", hero.Def.Accent);
        }

        HeroKind FreeKind(HeroKind start)
        {
            for (int i = 0; i < HeroDef.Count; i++)
            {
                var k = (HeroKind)(((int)start + i) % HeroDef.Count);
                if (!Heroes.Exists(h => h.Def.Kind == k)) return k;
            }
            return start;
        }

        public void CycleHero(Hero h)
        {
            var next = (HeroKind)(((int)h.Def.Kind + 1) % HeroDef.Count);
            for (int i = 0; i < HeroDef.Count; i++)
            {
                if (!Heroes.Exists(o => o != h && o.Def.Kind == next)) break;
                next = (HeroKind)(((int)next + 1) % HeroDef.Count);
            }
            h.SetKind(next);
            Fx.Play("poof", h.Pos, scale: 0.5f);
            Popup(h.Pos + Vector3.up * 2.4f, h.Def.Name, h.Def.Accent);
        }

        // ---------------------------------------------------------------- floor flow

        IEnumerator RunFloor()
        {
            State = GameState.Playing;
            kills = 0; reactions = 0;
            int players = Heroes.Count;
            float count = 0.6f + 0.4f * players, hp = 1f + 0.25f * (players - 1);

            var waves = new[]
            {
                new[] { (EnemyKind.Shade, 6) },
                new[] { (EnemyKind.Shade, 8), (EnemyKind.Skullwing, 2) },
                new[] { (EnemyKind.Shade, 8), (EnemyKind.Skullwing, 3), (EnemyKind.EliteShade, 1) },
            };

            for (wave = 0; wave < waves.Length; wave++)
            {
                Banner(wave == waves.Length - 1 ? "FINAL WAVE" : $"WAVE {wave + 1}", 2f);
                yield return new WaitForSeconds(1.6f);
                foreach (var group in waves[wave])
                {
                    var kind = group.Item1; int baseN = group.Item2;
                    int n = kind == EnemyKind.EliteShade ? baseN : Mathf.Max(1, Mathf.RoundToInt(baseN * count));
                    for (int i = 0; i < n; i++)
                    {
                        StartCoroutine(SpawnOne(kind, hp));
                        yield return new WaitForSeconds(0.35f);
                    }
                }
                while (pendingSpawns > 0 || Enemies.Count > 0) yield return null;
                yield return new WaitForSeconds(0.8f);
            }

            State = GameState.Cleared;
            Banner("FLOOR 1 CLEARED", 999f);
            Lantern.BaseRadius = 6.5f; // the ember grows
        }

        IEnumerator SpawnOne(EnemyKind kind, float hpScale)
        {
            pendingSpawns++;
            Vector3 at = Arena.RandomEdge();
            for (int tries = 0; tries < 6; tries++) // not right on top of a hero
            {
                if (!Heroes.Exists(h => (h.Pos - at).sqrMagnitude < 9f)) break;
                at = Arena.RandomEdge();
            }
            Fx.Play("spawn-portal", at, scale: kind == EnemyKind.EliteShade ? 1f : 0.6f);
            yield return new WaitForSeconds(0.9f);
            if (State == GameState.Playing) Enemies.Add(Enemy.Spawn(kind, at, hpScale));
            pendingSpawns--;
        }

        void ResetRun()
        {
            StopAllCoroutines();
            foreach (var e in Enemies) if (e) Destroy(e.gameObject);
            Enemies.Clear();
            pendingSpawns = 0;
            foreach (var h in Heroes) { h.Downed = false; h.Hp = h.Def.MaxHp; h.SpecialCd = 0; }
            Lantern.Drop();
            State = GameState.Lobby;
            banner = null;
        }

        public void OnEnemyDied(Enemy e)
        {
            Enemies.Remove(e);
            kills++;
        }

        // ---------------------------------------------------------------- queries & combat helpers

        /// <summary>Nearest living enemy in range. With a facing, enemies in front are preferred.</summary>
        public Enemy NearestEnemy(Vector3 from, float range, Vector3 facing, List<Enemy> exclude = null, float minDot = -1f)
        {
            Enemy best = null; float bestScore = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Dead || (exclude != null && exclude.Contains(e))) continue;
                var to = e.Pos - from;
                float d = to.magnitude;
                if (d > range) continue;
                float dot = facing == Vector3.zero || d < 0.01f ? 1f : Vector3.Dot(to / d, facing);
                if (dot < minDot) continue;
                float score = d * (1.6f - 0.6f * dot);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        public Hero NearestDowned(Hero me, float reach)
        {
            foreach (var h in Heroes)
                if (h != me && h.Downed && (h.Pos - me.Pos).sqrMagnitude < reach * reach) return h;
            return null;
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

        public void HitStop(float t) => hitStopT = Mathf.Max(hitStopT, t);
        public void Shake(float t, float amp)
        {
            if (shakeT <= 0) shakeAmp = 0;
            shakeT = Mathf.Max(shakeT, t);
            shakeAmp = Mathf.Max(shakeAmp, amp);
        }

        public void Reaction(string name, Vector3 at, Color c)
        {
            reactions++;
            HitStop(0.1f);
            Shake(0.3f, 0.25f);
            pops.Add(new Pop { At = at + Vector3.up * 2.6f, Text = name + "!", Color = c, Size = 30 });
        }

        public void Popup(Vector3 at, string text, Color c) => pops.Add(new Pop { At = at, Text = text, Color = c, Size = 18 });

        public void DamageNumber(Vector3 at, float dmg) =>
            pops.Add(new Pop { At = at + (Vector3)(Random.insideUnitCircle * 0.3f), Text = Mathf.RoundToInt(dmg).ToString(), Color = new Color(1f, 0.95f, 0.85f), Size = 14 });

        void Banner(string text, float t) { banner = text; bannerT = t; }

        // ---------------------------------------------------------------- HUD (IMGUI placeholder; move to UI Toolkit later)

        GUIStyle label;

        void OnGUI()
        {
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float W = Screen.width / s, H = 720f;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };

            // world popups
            foreach (var p in pops)
            {
                var sp = cam.WorldToScreenPoint(p.At);
                if (sp.z < 0) continue;
                float a = 1f - Mathf.Clamp01((p.T - 0.7f) / 0.4f);
                Text(new Rect(sp.x / s - 120, (Screen.height - sp.y) / s - 20, 240, 40), p.Text, p.Color * new Color(1, 1, 1, a), (int)p.Size);
            }

            // player cards
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                float x = 16 + h.Slot * 220, y = H - 88;
                Fill(new Rect(x, y, 204, 72), new Color(0.05f, 0.04f, 0.09f, 0.85f));
                Fill(new Rect(x, y, 4, 72), h.Def.Accent);
                Text(new Rect(x + 12, y + 4, 190, 22), $"P{h.Slot + 1}  {h.Def.Name.ToUpper()}  <size=11>{h.Def.Role}</size>", Color.white, 15, TextAnchor.MiddleLeft);
                Fill(new Rect(x + 12, y + 30, 180, 10), new Color(0.2f, 0.1f, 0.12f));
                Fill(new Rect(x + 12, y + 30, 180 * Mathf.Clamp01(h.Hp / h.Def.MaxHp), 10), h.Downed ? Color.gray : new Color(0.9f, 0.3f, 0.35f));
                Fill(new Rect(x + 12, y + 44, 180, 6), new Color(0.15f, 0.15f, 0.2f));
                Fill(new Rect(x + 12, y + 44, 180 * h.SpecialReady, 6), h.SpecialReady >= 1 ? h.Def.Accent : h.Def.Accent * 0.5f);
                string status = h.Downed ? (h.ReviveProgress > 0 ? $"REVIVING {Mathf.RoundToInt(h.ReviveProgress * 100)}%" : "DOWN · ally hold Lantern button")
                    : h.Carrying ? "CARRYING THE LANTERN" : $"{h.Def.SpecialName} {(h.SpecialReady >= 1 ? "ready" : "")}";
                Text(new Rect(x + 12, y + 50, 190, 20), status, h.Downed ? new Color(1, 0.5f, 0.5f) : new Color(0.8f, 0.78f, 0.9f), 11, TextAnchor.MiddleLeft);
            }

            // top line
            if (State == GameState.Playing)
                Text(new Rect(0, 8, W, 26), $"FLOOR 1 · WAVE {Mathf.Min(wave + 1, 3)}/3 · {Enemies.Count + pendingSpawns} left · reactions {reactions}", new Color(0.85f, 0.82f, 0.95f), 15);

            if (State == GameState.Lobby)
            {
                Text(new Rect(0, 70, W, 70), "LANTERNFALL", Ember, 56);
                Text(new Rect(0, 136, W, 30), "Gamepad: press A / ✕ to join  ·  Keyboard: J (WASD player) or , (arrow-keys player)", Color.white, 16);
                Text(new Rect(0, 162, W, 26), "Special button cycles your hero here  ·  Start / Enter begins the descent", new Color(0.8f, 0.78f, 0.9f), 14);
                Text(new Rect(0, 186, W, 26), "In play: Attack X/J · Special Y/K · Lantern pick up / throw / revive A/L", new Color(0.65f, 0.62f, 0.78f), 13);
            }

            if (banner != null && bannerT > 0)
            {
                Text(new Rect(0, H * 0.32f, W, 70), banner, State == GameState.Defeat ? new Color(1, 0.4f, 0.45f) : Ember, 46);
                if (State == GameState.Cleared)
                    Text(new Rect(0, H * 0.32f + 64, W, 30), $"{kills} shades banished · {reactions} Kindred Reactions · the ember burns brighter  ·  Start / Enter to play again", Color.white, 16);
                if (State == GameState.Defeat)
                    Text(new Rect(0, H * 0.32f + 64, W, 30), "Start / Enter to try again", Color.white, 16);
            }
        }

        void Text(Rect r, string t, Color c, int size, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            label.fontSize = size;
            label.alignment = anchor;
            var shadow = r; shadow.x += 2; shadow.y += 2;
            label.normal.textColor = new Color(0, 0, 0, c.a * 0.8f);
            GUI.Label(shadow, t, label);
            label.normal.textColor = c;
            GUI.Label(r, t, label);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
