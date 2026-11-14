using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>Materials, generated meshes and small helpers. Everything is built in code so the game runs with no art assets.</summary>
    public static class Util
    {
        static readonly Dictionary<int, Material> lit = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> glow = new Dictionary<int, Material>();
        static Material litTemplate;
        static readonly Dictionary<string, Sprite> blobs = new Dictionary<string, Sprite>();
        public static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor");

        static int Key(Color c) => ((int)(c.r * 255) << 24) ^ ((int)(c.g * 255) << 16) ^ ((int)(c.b * 255) << 8) ^ (int)(c.a * 255);

        /// <summary>A lit, matte material in the active render pipeline's default shader.</summary>
        public static Material Lit(Color c)
        {
            if (lit.TryGetValue(Key(c), out var m)) return m;
            if (litTemplate == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                litTemplate = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(tmp);
            }
            m = new Material(litTemplate) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            lit[Key(c)] = m;
            return m;
        }

        /// <summary>An unlit material for glowing bits (eyes, crystals, embers).</summary>
        public static Material Glow(Color c)
        {
            if (glow.TryGetValue(Key(c), out var m)) return m;
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            m = new Material(sh) { color = c };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            glow[Key(c)] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static GameObject MeshObj(string name, Mesh mesh, Transform parent, Vector3 pos, Material mat, Vector3 euler = default, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>A capped cone/frustum standing on y = 0, used for robes, hoods, spikes and crowns.</summary>
        public static Mesh Frustum(float rBottom, float rTop, float height, int seg = 14)
        {
            string key = $"fr{rBottom:F3}_{rTop:F3}_{height:F3}_{seg}";
            if (meshes.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * rBottom); v.Add(d * rTop + Vector3.up * height);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            int cb = v.Count; v.Add(Vector3.zero); int ct = v.Count; v.Add(Vector3.up * height);
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { cb, a + 2, a });
                t.AddRange(new[] { ct, a + 1, a + 3 });
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        /// <summary>A flat ring (halo, ground markers) in the XZ plane.</summary>
        public static Mesh Ring(float rIn, float rOut, int seg = 32)
        {
            string key = $"ring{rIn:F3}_{rOut:F3}_{seg}";
            if (meshes.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * rIn); v.Add(d * rOut);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); // both sides
            }
            m = new Mesh { name = key };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        /// <summary>A soft round blob sprite (shadows, light pools, telegraphs).</summary>
        public static Sprite Blob(Color color, int size = 64, bool ring = false)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + size + ring;
            if (blobs.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    var c = color;
                    if (d > 1) c.a = 0;
                    else if (ring) c.a *= d > 0.88f ? 1f : Mathf.Round(d * d * 3) / 6f;
                    else c.a *= Mathf.Round((1 - d * d) * 4) / 4f;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f, 0, SpriteMeshType.FullRect);
            blobs[key] = s;
            return s;
        }

        /// <summary>A flat sprite lying on the floor, `radius` world units across its half-width.</summary>
        public static SpriteRenderer FloorDecal(Transform parent, Sprite s, float radius, int order = -5)
        {
            var go = new GameObject("decal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.03f, 0);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.transform.localScale = Vector3.one * radius;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            if (Fx.SpriteMaterial) sr.sharedMaterial = Fx.SpriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
