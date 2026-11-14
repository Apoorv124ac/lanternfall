using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>On-screen UI, drawn with IMGUI so it needs no prefabs. Laid out on a 1280x720 virtual canvas.</summary>
    public sealed partial class Game
    {
        struct Pop { public Vector3 At; public string Text; public Color Color; public float T, Size; }
        readonly List<Pop> pops = new List<Pop>();
        string banner, bannerSub; float bannerT;
        int titleClick = -1, offerClick = -1;
        GUIStyle label;
        float W, H;
        static readonly Color Ember = Util.Hex("#ffae35"), Panel = new Color(0.05f, 0.04f, 0.08f, 0.82f), Ink = new Color(0.9f, 0.88f, 0.96f), Muted = new Color(0.66f, 0.63f, 0.76f);

        public void Popup(Vector3 at, string text, Color c, int size = 18) => pops.Add(new Pop { At = at, Text = text, Color = c, Size = size });

        public void DamageNumber(Vector3 at, float dmg, Hero src)
        {
            var c = src != null ? Color.Lerp(Color.white, src.Def.Accent, 0.45f) : Color.white;
            pops.Add(new Pop { At = at + Random.insideUnitSphere * 0.3f, Text = Mathf.RoundToInt(dmg).ToString(), Color = c, Size = dmg >= 40 ? 20 : 14 });
        }

        public void Banner(string text, float t, string sub = null) { banner = text; bannerSub = sub; bannerT = t; }

        void TickPops(float udt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i]; p.T += udt; p.At += Vector3.up * udt * 0.9f; pops[i] = p;
                if (p.T > 1.1f) pops.RemoveAt(i);
            }
            bannerT -= udt;
        }

        void OnGUI()
        {
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            W = Screen.width / s; H = 720f;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };

            if (State == GameState.Title) { DrawTitle(); return; }

            DrawPops(s);
            DrawCrosshair();
            DrawLeader();
            DrawCompanions();
            DrawXp();
            DrawTop();
            DrawBanner();
            if (State == GameState.LevelUp) DrawLevelUp();
            if (State == GameState.Paused) DrawPause();
        }

        // ---------------------------------------------------------------- in-game

        void DrawPops(float s)
        {
            foreach (var p in pops)
            {
                var sp = CamRig.Cam.WorldToScreenPoint(p.At);
                if (sp.z < 0) continue;
                float a = 1f - Mathf.Clamp01((p.T - 0.7f) / 0.4f);
                float pop = 1f + Mathf.Max(0, 0.25f - p.T) * 2f;
                Text(new Rect(sp.x / s - 160, (Screen.height - sp.y) / s - 20, 320, 40), p.Text, p.Color * new Color(1, 1, 1, a), (int)(p.Size * pop));
            }
            // revive progress over downed heroes
            foreach (var h in Heroes)
            {
                if (!h.Downed) continue;
                var sp = CamRig.Cam.WorldToScreenPoint(h.Pos + Vector3.up * 1.2f);
                if (sp.z < 0) continue;
                var r = new Rect(sp.x / s - 40, (Screen.height - sp.y) / s, 80, 8);
                Fill(r, new Color(0, 0, 0, 0.6f));
                Fill(new Rect(r.x, r.y, 80 * Mathf.Max(h.ReviveProgress, h.DownedTime / 10f), 8), new Color(0.5f, 1f, 0.6f));
                if (!Leader.Downed && (Leader.Pos - h.Pos).magnitude < 2.2f && Leader.Input != null)
                    Text(new Rect(sp.x / s - 120, (Screen.height - sp.y) / s + 10, 240, 24), Leader.Input.UsesMouse ? "Hold F to revive" : "Hold A / ✕ to revive", Ink, 14);
            }
        }

        void DrawCrosshair()
        {
            if (State != GameState.Playing || Leader == null || Leader.Downed) return;
            var lockOn = CamRig.LockTarget();
            float cx = W / 2, cy = H / 2;
            var c = lockOn != null ? new Color(1f, 0.45f, 0.4f, 0.95f) : new Color(1, 1, 1, 0.7f);
            float g = lockOn != null ? 7 : 10;
            Fill(new Rect(cx - 1, cy - g - 7, 2, 7), c); Fill(new Rect(cx - 1, cy + g, 2, 7), c);
            Fill(new Rect(cx - g - 7, cy - 1, 7, 2), c); Fill(new Rect(cx + g, cy - 1, 7, 2), c);
            Fill(new Rect(cx - 1.5f, cy - 1.5f, 3, 3), c);
        }

        void DrawLeader()
        {
            var h = Leader; if (h == null) return;
            float x = 24, y = H - 132, w = 380;
            Fill(new Rect(x, y, w, 112), Panel);
            Fill(new Rect(x, y, 6, 112), h.Def.Accent);
            // portrait
            Fill(new Rect(x + 16, y + 14, 64, 64), h.Def.Look.Cloth);
            Fill(new Rect(x + 16, y + 14, 64, 10), h.Def.Accent);
            Text(new Rect(x + 16, y + 26, 64, 52), h.Def.Name.Substring(0, 1), Color.white, 38);
            Text(new Rect(x + 92, y + 10, 280, 24), $"{h.Def.Name.ToUpper()}  <size=12><color=#b8b0d0>{h.Def.Role}</color></size>", Color.white, 19, TextAnchor.MiddleLeft);
            // hp
            Bar(new Rect(x + 92, y + 38, 272, 16), h.Hp / h.MaxHp, h.Downed ? Color.gray : new Color(0.92f, 0.3f, 0.36f), $"{Mathf.CeilToInt(h.Hp)} / {Mathf.CeilToInt(h.MaxHp)}");
            // abilities
            bool mouse = h.Input != null && h.Input.UsesMouse;
            Ability(new Rect(x + 92, y + 62, 84, 40), h.Def.SpecialName, mouse ? "RMB" : "Y", h.SpecialReady, h.Def.Accent);
            Ability(new Rect(x + 182, y + 62, 100, 40), h.Def.UltName, mouse ? "Q" : "LB", h.UltCharge / 100f, Ember);
            Ability(new Rect(x + 288, y + 62, 76, 40), "Dodge", mouse ? "Space" : "B", 1f - Mathf.Clamp01(h.DodgeCd / 0.9f), new Color(0.7f, 0.85f, 1f));
            Text(new Rect(x + 16, y + 80, 64, 26), $"LV {Party.Level}", Ember, 16);
        }

        void Ability(Rect r, string name, string key, float ready, Color c)
        {
            Fill(r, new Color(1, 1, 1, 0.06f));
            Fill(new Rect(r.x, r.yMax - 4, r.width * Mathf.Clamp01(ready), 4), ready >= 1 ? c : c * 0.55f);
            if (ready >= 1) Fill(new Rect(r.x, r.y, r.width, 2), c);
            Text(new Rect(r.x + 4, r.y + 2, r.width - 8, 18), name, ready >= 1 ? Color.white : Muted, 11, TextAnchor.MiddleLeft);
            Text(new Rect(r.x + 4, r.y + 18, r.width - 8, 16), ready >= 1 ? $"[{key}] ready" : $"[{key}] {Mathf.RoundToInt(ready * 100)}%", ready >= 1 ? c : Muted, 10, TextAnchor.MiddleLeft);
        }

        void DrawCompanions()
        {
            float y = 76;
            foreach (var h in Heroes)
            {
                if (h == Leader) continue;
                var r = new Rect(24, y, 230, 48);
                Fill(r, Panel);
                Fill(new Rect(r.x, r.y, 4, r.height), h.Def.Accent);
                string who = h.IsAI ? "AI" : "P" + (Heroes.FindAll(o => !o.IsAI).IndexOf(h) + 1);
                Text(new Rect(r.x + 12, r.y + 2, 210, 20), $"{h.Def.Name}  <size=10><color=#9a92b8>{who} · {h.Def.Role}</color></size>", Color.white, 13, TextAnchor.MiddleLeft);
                Bar(new Rect(r.x + 12, r.y + 24, 206, 9), h.Hp / h.MaxHp, h.Downed ? Color.gray : new Color(0.92f, 0.3f, 0.36f), null);
                Fill(new Rect(r.x + 12, r.y + 37, 206 * h.UltCharge / 100f, 4), h.UltCharge >= 100 ? Ember : Ember * 0.5f);
                if (h.Downed) Text(new Rect(r.x + 12, r.y + 20, 206, 18), "DOWN", new Color(1, 0.5f, 0.5f), 12);
                y += 54;
            }
        }

        void DrawXp()
        {
            float w = 560, x = (W - w) / 2, y = H - 30;
            Fill(new Rect(x, y, w, 10), new Color(0, 0, 0, 0.55f));
            Fill(new Rect(x, y, w * Party.Xp / Party.XpToNext, 10), Ember);
            Text(new Rect(x, y - 22, w, 20), $"LEVEL {Party.Level}   ·   {Mathf.FloorToInt(Party.Xp)} / {Party.XpToNext} embers", Muted, 12);
        }

        void DrawTop()
        {
            var def = FloorDef.All[floor];
            Text(new Rect(0, 8, W, 22), def.Name.ToUpper(), Ink, 15);
            string sub = portalOpen ? "Walk into the portal to descend" : BossAlive ? "" : $"Wave {Mathf.Min(wave + 1, def.Waves.Length)} · {Enemies.Count + pendingSpawns} cultists remain";
            Text(new Rect(0, 30, W, 18), sub, Muted, 12);
            if (BossAlive)
            {
                float w = 640, x = (W - w) / 2;
                Text(new Rect(x, 52, w, 22), Boss.Phase2 ? "ECLIPSE HERALD · UNVEILED" : "ECLIPSE HERALD", Boss.Phase2 ? new Color(1f, 0.45f, 0.7f) : Ink, 16);
                Bar(new Rect(x, 76, w, 14), Boss.Hp / Boss.MaxHp, Boss.Phase2 ? new Color(1f, 0.3f, 0.6f) : new Color(0.65f, 0.3f, 0.9f), null);
            }
            Text(new Rect(W - 260, 8, 240, 22), $"{kills} banished · {reactions} reactions", Muted, 12, TextAnchor.MiddleRight);
        }

        void DrawBanner()
        {
            if (banner == null || bannerT <= 0) return;
            float a = Mathf.Clamp01(bannerT * 2f);
            var c = State == GameState.Defeat ? new Color(1f, 0.4f, 0.45f, a) : new Color(Ember.r, Ember.g, Ember.b, a);
            Text(new Rect(0, H * 0.28f, W, 64), banner, c, 44);
            if (bannerSub != null) Text(new Rect(0, H * 0.28f + 58, W, 28), bannerSub, new Color(1, 1, 1, a * 0.9f), 17);
            if (State == GameState.Victory)
                Text(new Rect(0, H * 0.28f + 96, W, 60), $"Level {Party.Level} · {kills} banished · {reactions} Kindred Reactions · {Mathf.FloorToInt(runTime / 60)}m {Mathf.FloorToInt(runTime % 60)}s\nPress Enter / A to return to the title", Ink, 16);
        }

        void DrawLevelUp()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.04f, 0.72f));
            Text(new Rect(0, 90, W, 60), $"LEVEL {Party.Level - pendingLevels + 1}", Ember, 48);
            Text(new Rect(0, 146, W, 26), "The ember answers. Choose a blessing for the whole party.", Ink, 16);
            float cw = 270, ch = 300, gap = 28, total = offer.Count * cw + (offer.Count - 1) * gap, x0 = (W - total) / 2, y = 200;
            for (int i = 0; i < offer.Count; i++)
            {
                var u = offer[i];
                var r = new Rect(x0 + i * (cw + gap), y - (i == offerSel ? 10 : 0), cw, ch);
                Fill(r, i == offerSel ? new Color(0.12f, 0.09f, 0.18f, 0.98f) : Panel);
                Fill(new Rect(r.x, r.y, r.width, 8), u.Color);
                if (i == offerSel) { Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, 3), u.Color); Fill(new Rect(r.x - 3, r.yMax, r.width + 6, 3), u.Color); Fill(new Rect(r.x - 3, r.y, 3, r.height), u.Color); Fill(new Rect(r.xMax, r.y, 3, r.height), u.Color); }
                // emblem
                Fill(new Rect(r.center.x - 34, r.y + 40, 68, 68), u.Color * new Color(1, 1, 1, 0.25f));
                Fill(new Rect(r.center.x - 20, r.y + 54, 40, 40), u.Color);
                Text(new Rect(r.x + 12, r.y + 124, r.width - 24, 34), u.Name.ToUpper(), Color.white, 21);
                Text(new Rect(r.x + 20, r.y + 162, r.width - 40, 70), u.Desc, Ink, 15);
                Party.Taken.TryGetValue(u.Id, out int k);
                Text(new Rect(r.x, r.yMax - 46, r.width, 22), $"{k} / {u.MaxStacks}", Muted, 12);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) offerClick = i;
            }
            bool mouse = Leader.Input != null && Leader.Input.UsesMouse;
            Text(new Rect(0, y + ch + 30, W, 24), mouse ? "A / D to choose · Enter or click to take" : "Left / Right to choose · A / ✕ to take", Muted, 14);
        }

        void DrawPause()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.01f, 0.04f, 0.75f));
            Text(new Rect(0, 110, W, 60), "PAUSED", Ember, 46);
            string[] rows =
            {
                "<b>Move</b>  WASD / left stick",
                "<b>Camera</b>  mouse / right stick",
                "<b>Attack</b>  left mouse / X or RT (hold)",
                "<b>Special</b>  right mouse or E / Y or RB",
                "<b>Ultimate</b>  Q / LB or LT (charges as you deal damage)",
                "<b>Dodge roll</b>  Space / B (you can't be hit mid-roll)",
                "<b>Revive an ally</b>  hold F / hold A",
                "<b>Friends</b>  a second gamepad presses A to take over an AI hero",
                "",
                "Red circles on the floor are incoming attacks: roll out of them.",
                "Fire or lightning on a frozen enemy SHATTERS it. Lightning on a burning one OVERLOADS.",
                "Stand in the Lantern's light to heal. Enemies outside it hit harder.",
                "",
                "Esc / Start to resume",
            };
            for (int i = 0; i < rows.Length; i++) Text(new Rect(0, 190 + i * 28, W, 26), rows[i], i >= 9 ? Muted : Ink, 16);
        }

        // ---------------------------------------------------------------- title

        void DrawTitle()
        {
            Fill(new Rect(0, 0, W, 160), new Color(0, 0, 0, 0.35f));
            Text(new Rect(0, 30, W, 80), "LANTERNFALL", Ember, 72);
            Text(new Rect(0, 104, W, 26), "The Eclipse Maw swallowed the sun. Carry the last ember down four floors and light it again.", Ink, 16);

            float cw = 270, ch = 210, gap = 16, total = 4 * cw + 3 * gap, x0 = (W - total) / 2, y = H - ch - 70;
            for (int i = 0; i < HeroDef.Count; i++)
            {
                var d = HeroDef.Get((HeroKind)i);
                bool sel = i == (int)titlePick;
                var r = new Rect(x0 + i * (cw + gap), y - (sel ? 12 : 0), cw, ch);
                Fill(r, sel ? new Color(0.1f, 0.08f, 0.15f, 0.95f) : Panel);
                Fill(new Rect(r.x, r.y, r.width, sel ? 8 : 4), d.Accent);
                Text(new Rect(r.x + 14, r.y + 14, r.width - 28, 30), d.Name.ToUpper(), sel ? d.Accent : Color.white, 24, TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 14, r.y + 42, r.width - 28, 18), d.Role, Muted, 12, TextAnchor.MiddleLeft);
                Text(new Rect(r.x + 14, r.y + 62, r.width - 28, 64), d.Blurb, Ink, 12, TextAnchor.UpperLeft);
                Text(new Rect(r.x + 14, r.y + 132, r.width - 28, 70),
                    $"<color=#9a92b8>Attack</color>  {d.AttackName}\n<color=#9a92b8>Special</color>  {d.SpecialName}\n<color=#9a92b8>Ultimate</color>  {d.UltName}", Ink, 12, TextAnchor.UpperLeft);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) titleClick = i;
            }
            float blink = 0.6f + Mathf.Sin(runTime * 4f) * 0.4f;
            Text(new Rect(0, H - 52, W, 24), "← →  choose your hero   ·   Enter / Space / A to begin   ·   the other three fight beside you", new Color(1, 1, 1, blink), 15);
        }

        // ---------------------------------------------------------------- primitives

        void Bar(Rect r, float v, Color c, string txt)
        {
            Fill(r, new Color(0, 0, 0, 0.55f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), c);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), Mathf.Max(1, r.height * 0.25f)), new Color(1, 1, 1, 0.25f));
            if (txt != null) Text(r, txt, Color.white, 11);
        }

        void Text(Rect r, string t, Color c, int size, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            label.fontSize = size;
            label.alignment = anchor;
            var sh = r; sh.x += 2; sh.y += 2;
            label.normal.textColor = new Color(0, 0, 0, c.a * 0.75f);
            GUI.Label(sh, t, label);
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
