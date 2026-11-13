using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>Floor one of the descent: a ruined plaza with broken pillars. Built from primitives so it works in any render pipeline.</summary>
    public sealed class Arena
    {
        public readonly float HalfW = 13f, HalfD = 8f;
        public readonly List<Vector4> Pillars = new List<Vector4>(); // x, z, radius
        public Transform Root;

        public Arena(Transform parent)
        {
            Root = new GameObject("Arena").transform;
            Root.SetParent(parent, false);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(Root, false);
            floor.transform.localScale = new Vector3(HalfW * 2 + 2, 1, HalfD * 2 + 2);
            floor.transform.localPosition = new Vector3(0, -0.5f, 0);
            Paint(floor, new Color(0.16f, 0.15f, 0.2f));
            Object.Destroy(floor.GetComponent<Collider>());

            // checker tiles give the eye a sense of depth and speed
            for (int x = -6; x <= 6; x++)
                for (int z = -4; z <= 4; z++)
                {
                    if (((x + z) & 1) == 0) continue;
                    var t = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    t.name = "tile";
                    t.transform.SetParent(Root, false);
                    t.transform.localPosition = new Vector3(x * 2, 0.005f, z * 2);
                    t.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    t.transform.localScale = new Vector3(2, 2, 1);
                    Paint(t, new Color(0.19f, 0.18f, 0.24f));
                    Object.Destroy(t.GetComponent<Collider>());
                }

            // back wall + side walls
            Wall(new Vector3(0, 1.5f, HalfD + 1.2f), new Vector3(HalfW * 2 + 4, 3, 1));
            Wall(new Vector3(-HalfW - 1.5f, 0.6f, 0), new Vector3(1, 1.2f, HalfD * 2 + 3));
            Wall(new Vector3(HalfW + 1.5f, 0.6f, 0), new Vector3(1, 1.2f, HalfD * 2 + 3));

            Pillar(-7, 3.5f, 0.75f, 3.2f);
            Pillar(7, 3.5f, 0.75f, 2.2f);   // broken
            Pillar(-7, -4, 0.75f, 1.4f);    // stump
            Pillar(7.5f, -3.5f, 0.75f, 3.2f);
        }

        void Wall(Vector3 pos, Vector3 size)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Wall";
            w.transform.SetParent(Root, false);
            w.transform.localPosition = pos;
            w.transform.localScale = size;
            Paint(w, new Color(0.11f, 0.1f, 0.15f));
            Object.Destroy(w.GetComponent<Collider>());
        }

        void Pillar(float x, float z, float r, float h)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Pillar";
            p.transform.SetParent(Root, false);
            p.transform.localPosition = new Vector3(x, h / 2, z);
            p.transform.localScale = new Vector3(r * 2, h / 2, r * 2);
            Paint(p, new Color(0.3f, 0.27f, 0.36f));
            Object.Destroy(p.GetComponent<Collider>());
            Pillars.Add(new Vector4(x, z, r, h));
        }

        static void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            var m = new Material(r.sharedMaterial) { color = c };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            r.sharedMaterial = m;
        }

        public Vector3 Resolve(Vector3 p, float radius)
        {
            p.x = Mathf.Clamp(p.x, -HalfW + radius, HalfW - radius);
            p.z = Mathf.Clamp(p.z, -HalfD + radius, HalfD - radius);
            foreach (var pl in Pillars)
            {
                var d = new Vector2(p.x - pl.x, p.z - pl.y);
                float min = pl.z + radius;
                if (d.sqrMagnitude < min * min)
                {
                    if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
                    d = d.normalized * min;
                    p.x = pl.x + d.x; p.z = pl.y + d.y;
                }
            }
            return p;
        }

        public Vector3 Clamp(Vector3 p, float margin) =>
            new Vector3(Mathf.Clamp(p.x, -HalfW + margin, HalfW - margin), 0, Mathf.Clamp(p.z, -HalfD + margin, HalfD - margin));

        public Vector3 RandomEdge()
        {
            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: return new Vector3(-HalfW + 1, 0, Random.Range(-HalfD + 1, HalfD - 1));
                case 1: return new Vector3(HalfW - 1, 0, Random.Range(-HalfD + 1, HalfD - 1));
                case 2: return new Vector3(Random.Range(-HalfW + 1, HalfW - 1), 0, HalfD - 1);
                default: return new Vector3(Random.Range(-HalfW + 1, HalfW - 1), 0, -HalfD + 1);
            }
        }
    }
}
