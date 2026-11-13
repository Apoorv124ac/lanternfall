using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>
    /// Placeholder pixel art drawn in code at pixelfx scale (heroes 24x38), so the slice runs with no art assets.
    /// Swap these for Aseprite sheets later; everything else only needs a Sprite[].
    /// </summary>
    public static class PixelArt
    {
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        static readonly Color Clear = new Color(0, 0, 0, 0);
        static readonly Color Outline = new Color32(14, 10, 22, 255);

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;
            public Canvas(int w, int h) { W = w; H = h; Px = new Color[w * h]; for (int i = 0; i < Px.Length; i++) Px[i] = Clear; }
            // y = 0 is the bottom row (Unity texture space)
            public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }
            public Color Get(int x, int y) => (x >= 0 && y >= 0 && x < W && y < H) ? Px[y * W + x] : Clear;
            public void Rect(int x0, int y0, int x1, int y1, Color c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c); }
            public void Disc(float cx, float cy, float r, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                    for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(x, y, c);
            }
            public void Outline(Color o)
            {
                var copy = (Color[])Px.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (copy[y * W + x].a > 0) continue;
                        bool edge = false;
                        for (int k = 0; k < 4 && !edge; k++)
                        {
                            int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            if (nx >= 0 && ny >= 0 && nx < W && ny < H && copy[ny * W + nx].a > 0) edge = true;
                        }
                        if (edge) Px[y * W + x] = o;
                    }
            }
            public Sprite ToSprite(string name, Vector2 pivot)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
                tex.SetPixels(Px);
                tex.Apply(false, true);
                var s = Sprite.Create(tex, new Rect(0, 0, W, H), pivot, Fx.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                s.name = name;
                return s;
            }
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        /// <summary>Two walk frames (0 = stand, 1 = stride) for a hero, feet at the pivot.</summary>
        public static Sprite[] Hero(HeroKind kind)
        {
            string key = "hero " + kind;
            if (cache.TryGetValue(key, out var s)) return s;
            var def = HeroDef.Get(kind);
            s = new Sprite[2];
            for (int f = 0; f < 2; f++) s[f] = DrawHero(def, f).ToSprite($"{kind} {f}", new Vector2(0.5f, 1f / 40f));
            cache[key] = s;
            return s;
        }

        static Canvas DrawHero(HeroDef d, int frame)
        {
            var c = new Canvas(26, 40);
            Color cloak = d.Cloak, shade = d.Cloak * 0.65f, trim = d.Accent, skin = Hex("#f2c7a5"), dark = Hex("#1b1424");
            shade.a = 1;
            int cx = 13;
            // legs / boots
            int stride = frame == 1 ? 2 : 0;
            c.Rect(cx - 4 - stride, 1, cx - 2 - stride, 5, dark);
            c.Rect(cx + 1 + stride, 1, cx + 3 + stride, 5, dark);
            // robe: widens toward the hem
            for (int y = 4; y <= 23; y++)
            {
                int half = 7 - (y - 4) / 5;
                for (int x = cx - half; x <= cx + half; x++) c.Set(x, y, x < cx - half / 2 ? shade : cloak);
            }
            c.Rect(cx - 6, 4, cx + 6, 5, trim);          // hem trim
            c.Rect(cx - 4, 15, cx + 4, 16, trim);        // belt
            // arms
            c.Rect(cx - 7, 12, cx - 6, 20, shade);
            c.Rect(cx + 6, 12, cx + 7, 20, cloak);
            c.Rect(cx + 6, 11, cx + 7, 12, skin);
            // head + hood
            c.Disc(cx, 28.5f, 5.5f, cloak);
            c.Disc(cx + 1, 27.5f, 3.6f, skin);
            c.Rect(cx - 5, 22, cx - 4, 27, shade);
            c.Set(cx + 1, 28, dark); c.Set(cx + 3, 28, dark);   // eyes, facing right
            c.Set(cx + 2, 25, Hex("#c98f78"));
            // element mark
            switch (d.Kind)
            {
                case HeroKind.Cinder: // flame tuft
                    c.Rect(cx - 1, 34, cx + 1, 35, Hex("#ff7a1c")); c.Set(cx, 36, Hex("#ffd34a")); c.Set(cx + 2, 35, Hex("#ffd34a")); c.Set(cx - 2, 34, Hex("#ff7a1c"));
                    break;
                case HeroKind.Rime: // ice crown
                    for (int i = -2; i <= 2; i += 2) { c.Set(cx + i, 34, Hex("#bff1ff")); c.Set(cx + i, 35, Hex("#ffffff")); }
                    c.Rect(cx - 7, 21, cx - 5, 24, Hex("#8fdcff")); // shoulder plate
                    break;
                case HeroKind.Volt: // lightning antenna
                    c.Set(cx + 2, 34, Hex("#fff35c")); c.Set(cx + 3, 35, Hex("#fff35c")); c.Set(cx + 2, 36, Hex("#fff35c")); c.Set(cx + 3, 37, Hex("#ffffff"));
                    break;
                case HeroKind.Sol: // halo
                    for (int x = cx - 4; x <= cx + 4; x++) { c.Set(x, 36, Hex("#ffe58a")); }
                    c.Set(cx - 5, 35, Hex("#ffe58a")); c.Set(cx + 5, 35, Hex("#ffe58a"));
                    break;
            }
            c.Outline(Outline);
            return c;
        }

        /// <summary>Eclipse shade: a hungry dark blob with two ember eyes. Frame 1 squashes.</summary>
        public static Sprite[] Shade(bool elite)
        {
            string key = elite ? "shade elite" : "shade";
            if (cache.TryGetValue(key, out var s)) return s;
            s = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                int w = elite ? 34 : 22, h = elite ? 30 : 20;
                var c = new Canvas(w, h);
                Color body = elite ? Hex("#3a1450") : Hex("#24163a"), rim = elite ? Hex("#7b2f9a") : Hex("#4a2f70"), eye = elite ? Hex("#ff4f6a") : Hex("#ffb347");
                float rx = w / 2f - 2 + f, ry = h / 2f - 2 - f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f - w / 2f) / rx, dy = (y + 0.5f - (ry + 1)) / ry;
                        if (dx * dx + dy * dy > 1) continue;
                        if (y < 3 && ((x + f) % 4 == 0)) continue; // ragged hem
                        c.Set(x, y, dy > 0.45f ? rim : body);
                    }
                int ey = Mathf.RoundToInt(ry + 2), e = elite ? 2 : 1;
                c.Rect(w / 2 - 3 - e, ey, w / 2 - 3, ey + e, eye);
                c.Rect(w / 2 + 2, ey, w / 2 + 2 + e, ey + e, eye);
                c.Outline(Outline);
                s[f] = c.ToSprite(key + " " + f, new Vector2(0.5f, 0f));
            }
            cache[key] = s;
            return s;
        }

        /// <summary>Skullwing: a flying skull that hunts the Lantern carrier. Frames flap.</summary>
        public static Sprite[] Skullwing()
        {
            if (cache.TryGetValue("skullwing", out var s)) return s;
            s = new Sprite[2];
            Color bone = Hex("#e8e0cf"), boneShade = Hex("#a99f8c"), wing = Hex("#3b2c52"), eye = Hex("#7dfcff");
            for (int f = 0; f < 2; f++)
            {
                var c = new Canvas(26, 16);
                int wy = f == 0 ? 10 : 4;
                for (int i = 0; i < 7; i++) { c.Rect(i, wy - i / 3, i, 8, wing); c.Rect(25 - i, wy - i / 3, 25 - i, 8, wing); }
                c.Disc(13, 8, 5f, bone);
                c.Rect(10, 2, 16, 4, boneShade);
                c.Set(11, 8, eye); c.Set(15, 8, eye); c.Set(11, 9, eye); c.Set(15, 9, eye);
                c.Set(11, 3, Outline); c.Set(13, 3, Outline); c.Set(15, 3, Outline);
                c.Outline(Outline);
                s[f] = c.ToSprite("skullwing " + f, new Vector2(0.5f, 0f));
            }
            cache["skullwing"] = s;
            return s;
        }

        /// <summary>The Lantern's brass frame; the ember inside is pixelfx fire/spirit-flame.</summary>
        public static Sprite Lantern()
        {
            if (cache.TryGetValue("lantern", out var s)) return s[0];
            var c = new Canvas(14, 20);
            Color brass = Hex("#c9923a"), brassDark = Hex("#7a5322"), glass = Hex("#ffe9a8");
            c.Rect(2, 0, 11, 2, brassDark);
            c.Rect(3, 3, 10, 13, glass);
            c.Rect(2, 3, 2, 13, brass); c.Rect(11, 3, 11, 13, brass);
            c.Rect(2, 14, 11, 15, brass);
            c.Rect(5, 16, 8, 17, brassDark);
            c.Rect(6, 18, 7, 19, brass);
            c.Outline(Outline);
            var sp = c.ToSprite("lantern", new Vector2(0.5f, 0f));
            cache["lantern"] = new[] { sp };
            return sp;
        }

        /// <summary>A soft round blob shadow / light pool.</summary>
        public static Sprite Blob(Color color, int size = 32)
        {
            string key = "blob " + ColorUtility.ToHtmlStringRGBA(color) + size;
            if (cache.TryGetValue(key, out var s)) return s[0];
            var c = new Canvas(size, size);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    if (d > 1) continue;
                    var col = color; col.a *= Mathf.Round((1 - d * d) * 4) / 4f; // banded, pixel-art style falloff
                    c.Set(x, y, col);
                }
            var sp = c.ToSprite(key, new Vector2(0.5f, 0.5f));
            cache[key] = new[] { sp };
            return sp;
        }
    }
}
