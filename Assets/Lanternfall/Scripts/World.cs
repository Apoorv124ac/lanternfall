using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    public enum EnemyKind { Cultist, Gunner, Brute, Boss }

    public sealed class FloorDef
    {
        public string Name, Subtitle;
        public Color Ground, Tile, Stone, Fog, Sun, Ambient, Accent;
        public float FogDensity = 0.025f;
        public Group[][] Waves;
        public struct Group { public EnemyKind kind; public int count; }
        static Group G(EnemyKind k, int n) => new Group { kind = k, count = n };
        public bool Boss;

        public static readonly FloorDef[] All =
        {
            new FloorDef { Name = "Floor 1 · The Ashen Gate", Subtitle = "Where the last sunlight fell",
                Ground = Util.Hex("#2b2622"), Tile = Util.Hex("#36302a"), Stone = Util.Hex("#5a5049"), Fog = Util.Hex("#2a1a14"), Sun = Util.Hex("#ff9a5a"), Ambient = Util.Hex("#3a2a26"), Accent = Util.Hex("#ff8a3a"),
                Waves = new[] {
                    new[] { G(EnemyKind.Cultist, 4) },
                    new[] { G(EnemyKind.Cultist, 6) },
                    new[] { G(EnemyKind.Cultist, 5), G(EnemyKind.Gunner, 2) } } },
            new FloorDef { Name = "Floor 2 · The Frozen Cloister", Subtitle = "The monks who stopped praying",
                Ground = Util.Hex("#26303a"), Tile = Util.Hex("#2f3b48"), Stone = Util.Hex("#6f8396"), Fog = Util.Hex("#1a2a3a"), Sun = Util.Hex("#9fd2ff"), Ambient = Util.Hex("#26364a"), Accent = Util.Hex("#7fd6ff"),
                Waves = new[] {
                    new[] { G(EnemyKind.Cultist, 5), G(EnemyKind.Gunner, 2) },
                    new[] { G(EnemyKind.Cultist, 6), G(EnemyKind.Gunner, 3) },
                    new[] { G(EnemyKind.Brute, 1), G(EnemyKind.Cultist, 5) } } },
            new FloorDef { Name = "Floor 3 · The Storm Spire", Subtitle = "Thunder without a sky",
                Ground = Util.Hex("#26223a"), Tile = Util.Hex("#302a48"), Stone = Util.Hex("#5d5480"), Fog = Util.Hex("#1c1530"), Sun = Util.Hex("#c8b0ff"), Ambient = Util.Hex("#2a2445"), Accent = Util.Hex("#fff05a"),
                Waves = new[] {
                    new[] { G(EnemyKind.Cultist, 7), G(EnemyKind.Gunner, 3) },
                    new[] { G(EnemyKind.Brute, 2), G(EnemyKind.Cultist, 4) },
                    new[] { G(EnemyKind.Brute, 1), G(EnemyKind.Cultist, 8), G(EnemyKind.Gunner, 4) } } },
            new FloorDef { Name = "Floor 4 · The Maw's Throne", Subtitle = "Light the sun again",
                Ground = Util.Hex("#1a1420"), Tile = Util.Hex("#241a2c"), Stone = Util.Hex("#4a3550"), Fog = Util.Hex("#12081a"), Sun = Util.Hex("#ff5a8a"), Ambient = Util.Hex("#24142c"), Accent = Util.Hex("#ff4f9a"),
                Boss = true, FogDensity = 0.03f,
                Waves = new[] {
                    new[] { G(EnemyKind.Cultist, 6), G(EnemyKind.Gunner, 3) },
                    new[] { G(EnemyKind.Boss, 1) } } },
        };
    }

    /// <summary>A round courtyard built from primitives, themed per floor. Circle collision for the edge and pillars.</summary>
    public sealed class Arena
    {
        public const float Radius = 17f;
        public readonly List<Vector3> Pillars = new List<Vector3>(); // x, z, radius
        public readonly Transform Root;
        public readonly FloorDef Def;

        public Arena(FloorDef def, Transform parent)
        {
            Def = def;
            Root = new GameObject("Arena " + def.Name).transform;
            Root.SetParent(parent, false);
            Material ground = Util.Lit(def.Ground), stone = Util.Lit(def.Stone), stoneDark = Util.Lit(def.Stone * 0.6f + new Color(0, 0, 0, 0.4f));

            // floor disc + outer ground
            Util.MeshObj("Floor", Util.Frustum(Radius + 1.5f, Radius + 1.5f, 0.2f, 48), Root, new Vector3(0, -0.2f, 0), ground);
            Util.MeshObj("Outer", Util.Frustum(60, 60, 0.2f, 24), Root, new Vector3(0, -0.45f, 0), Util.Lit(def.Ground * 0.5f + new Color(0, 0, 0, 0.5f)));
            // dark tactical grid floor (like the devlog greybox): 1 m cells, a brighter line every 4 m
            var grid = new Material(Util.Lit(Color.white));
            grid.mainTexture = GridTexture(def.Ground, Color.Lerp(def.Stone, def.Accent, 0.2f));
            grid.mainTextureScale = new Vector2(0.25f, 0.25f);
            Util.MeshObj("Grid", Disc(Radius + 1.5f, 64), Root, new Vector3(0, 0.005f, 0), grid);
            Util.MeshObj("Sigil", Util.Ring(1.6f, 1.9f, 40), Root, new Vector3(0, 0.02f, 0), Util.Glow(def.Accent * 0.7f + new Color(0, 0, 0, 0.3f)));
            Util.MeshObj("EdgeLine", Util.Ring(Radius - 0.1f, Radius + 0.05f, 64), Root, new Vector3(0, 0.02f, 0), Util.Glow(def.Accent * 0.45f + new Color(0, 0, 0, 0.55f)));

            // ring wall of columns and broken arches
            int cols = 22;
            for (int i = 0; i < cols; i++)
            {
                float a = i / (float)cols * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius + 1.3f);
                float h = (i % 3 == 0) ? 2.2f : 5.5f + (i % 2) * 1.2f;
                if (p.z < 2f) h = Mathf.Min(h, 1.3f); // keep the camera side low so walls never hide the squad
                Column(p, 0.7f, h, stone, stoneDark, true);
                if (i % 2 == 0 && p.z > 2f)
                {
                    var q = new Vector3(Mathf.Cos(a + Mathf.PI / cols), 0, Mathf.Sin(a + Mathf.PI / cols)) * (Radius + 1.6f);
                    Util.Prim(PrimitiveType.Cube, Root, q + Vector3.up * 0.9f, new Vector3(2.6f, 1.8f, 0.6f), stoneDark, new Vector3(0, -(a + Mathf.PI / cols) * Mathf.Rad2Deg + 90, 0));
                }
            }
            // inner pillars to fight around
            float[] ang = { 30, 105, 165, 230, 300 };
            foreach (var d in ang)
            {
                float a = d * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9.5f;
                Column(p, 0.85f, d == 165 ? 1.2f : 2.4f, stone, stoneDark, false); // low cover blocks, so the top-down camera always sees you
                Pillars.Add(new Vector3(p.x, p.z, 1.0f));
            }
            // braziers with pixelfx flames + warm lights
            for (int i = 0; i < 4; i++)
            {
                float a = (45 + i * 90) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 14.5f;
                Util.MeshObj("Brazier", Util.Frustum(0.25f, 0.55f, 1.0f, 10), Root, p, stoneDark);
                var flame = Fx.Play("spirit-flame", p + Vector3.up * 1.0f, scale: 0.9f, loop: true);
                if (flame) flame.transform.SetParent(Root, true);
                var l = new GameObject("Brazier Light").AddComponent<Light>();
                l.transform.SetParent(Root, false);
                l.transform.localPosition = p + Vector3.up * 2f;
                l.type = LightType.Point; l.range = 9; l.intensity = 1.6f; l.color = def.Accent;
                Pillars.Add(new Vector3(p.x, p.z, 0.6f));
            }
            // a throne / gate at the north edge for the boss floor feel
            Util.Prim(PrimitiveType.Cube, Root, new Vector3(0, 3.5f, Radius + 2.5f), new Vector3(7, 7, 1), stoneDark);
            Util.MeshObj("GateGlow", Util.Ring(1.8f, 2.4f, 32), Root, new Vector3(0, 4.2f, Radius + 1.95f), Util.Glow(def.Accent), new Vector3(90, 0, 0));
        }

        /// <summary>Flat disc with world-space XZ UVs so a tiled texture lines up in metres.</summary>
        static Mesh Disc(float r, int seg)
        {
            var v = new Vector3[seg + 1]; var uv = new Vector2[seg + 1]; var tri = new int[seg * 3];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                v[i] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                uv[i] = new Vector2(v[i].x, v[i].z);
                tri[i * 3] = seg; tri[i * 3 + 1] = (i + 1) % seg; tri[i * 3 + 2] = i;
            }
            var m = new Mesh { vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A 4x4-metre grid tile: thin lines each metre, a bold line on the border.</summary>
        static Texture2D GridTexture(Color ground, Color line)
        {
            const int N = 256, cell = N / 4;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var bg = Color.Lerp(ground, new Color(0.08f, 0.08f, 0.1f), 0.55f);
            var thin = Color.Lerp(bg, line, 0.45f);
            var bold = Color.Lerp(bg, line, 0.9f);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool b = x < 3 || y < 3;
                    bool t = (x % cell) < 2 || (y % cell) < 2;
                    px[y * N + x] = b ? bold : t ? thin : bg;
                }
            tex.SetPixels(px); tex.Apply(true);
            return tex;
        }

        void Column(Vector3 p, float r, float h, Material stone, Material dark, bool outer)
        {
            Util.MeshObj("Base", Util.Frustum(r * 1.35f, r * 1.2f, 0.35f, 12), Root, p, dark);
            Util.MeshObj("Shaft", Util.Frustum(r, r * 0.88f, h, 12), Root, p + Vector3.up * 0.35f, stone);
            Util.MeshObj("Cap", Util.Frustum(r * 1.25f, r * 1.4f, 0.3f, 12), Root, p + Vector3.up * (0.35f + h), dark);
        }

        public Vector3 Resolve(Vector3 p, float radius)
        {
            p.y = 0;
            var flat = new Vector2(p.x, p.z);
            float max = Radius - radius;
            if (flat.magnitude > max) flat = flat.normalized * max;
            foreach (var pl in Pillars)
            {
                var c = new Vector2(pl.x, pl.y);
                var d = flat - c;
                float min = pl.z + radius;
                if (d.sqrMagnitude < min * min)
                {
                    if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
                    flat = c + d.normalized * min;
                }
            }
            return new Vector3(flat.x, 0, flat.y);
        }

        public Vector3 RandomEdge(Vector3 awayFrom)
        {
            Vector3 best = Vector3.zero; float bd = -1;
            for (int i = 0; i < 5; i++)
            {
                float a = Random.value * Mathf.PI * 2;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Radius - 2f);
                float d = (p - awayFrom).sqrMagnitude;
                if (d > bd) { bd = d; best = p; }
            }
            return best;
        }

        public void Destroy() => Object.Destroy(Root.gameObject);
    }
}
