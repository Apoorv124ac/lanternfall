using UnityEngine;

namespace Lanternfall
{
    /// <summary>
    /// The last ember. Lights the arena around it; anyone can pick it up (slower, no abilities) and throw it.
    /// Sol's Dawnlight: while Sol carries it, the light reaches 50% further.
    /// </summary>
    public sealed class Lantern : MonoBehaviour
    {
        public Hero Carrier { get; private set; }
        public float BaseRadius = 5.5f;
        public float Radius => BaseRadius * (Carrier != null && Carrier.Def.Kind == HeroKind.Sol ? 1.5f : 1f);

        Light pointLight;
        Transform frameT, pool;
        SpriteRenderer poolSr;
        float shownRadius;
        bool flying;
        Vector3 flyFrom, flyTo;
        float flyT;
        const float FlyTime = 0.55f;

        public Vector3 Pos => new Vector3(transform.position.x, 0, transform.position.z);

        public static Lantern Create(Vector3 pos)
        {
            var go = new GameObject("Lantern");
            go.transform.position = pos;
            var l = go.AddComponent<Lantern>();
            l.Build();
            return l;
        }

        void Build()
        {
            var lightGo = new GameObject("Ember Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0, 2.2f, 0);
            pointLight = lightGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.72f, 0.38f);
            pointLight.intensity = 6f;
            pointLight.shadows = LightShadows.None;

            // the visible pool of light on the floor (works in any pipeline, and doubles as the safe-zone readout)
            var poolGo = new GameObject("Light Pool");
            pool = poolGo.transform;
            pool.SetParent(transform, false);
            pool.localPosition = new Vector3(0, 0.015f, 0);
            pool.localRotation = Quaternion.Euler(90, 0, 0);
            poolSr = poolGo.AddComponent<SpriteRenderer>();
            poolSr.sprite = PixelArt.Blob(new Color(1f, 0.68f, 0.3f, 0.32f), 64);
            if (Fx.SpriteMaterial) poolSr.sharedMaterial = Fx.SpriteMaterial;
            poolSr.sortingOrder = -10;

            var frameGo = new GameObject("Frame");
            frameT = frameGo.transform;
            frameT.SetParent(transform, false);
            var sr = frameGo.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.Lantern();
            if (Fx.SpriteMaterial) sr.sharedMaterial = Fx.SpriteMaterial;
            sr.sortingOrder = 6;
            frameGo.AddComponent<Billboard>();

            Fx.Play("spirit-flame", transform.position + Vector3.up * 0.2f, follow: frameT, scale: 0.35f, loop: true);
            shownRadius = BaseRadius;
        }

        /// <summary>1 inside the light, fading to 0 at its edge.</summary>
        public float LightAt(Vector3 p)
        {
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(transform.position.x, transform.position.z));
            return 1f - Mathf.Clamp01((d - shownRadius * 0.75f) / (shownRadius * 0.35f));
        }

        public bool CanPickUp(Vector3 p, float reach) =>
            Carrier == null && !flying && (p - Pos).sqrMagnitude < reach * reach;

        public void PickUp(Hero h)
        {
            Carrier = h;
            Game.I.Popup(h.Pos + Vector3.up * 2.4f, h.Def.Kind == HeroKind.Sol ? "DAWNLIGHT" : "LANTERN", new Color(1f, 0.8f, 0.4f));
        }

        public void Drop() { Carrier = null; }

        public void Throw(Vector3 dir)
        {
            var from = Pos;
            Carrier = null;
            flying = true;
            flyT = 0;
            flyFrom = from;
            flyTo = Game.I.Arena.Resolve(from + dir.normalized * 6.5f, 0.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 frameLocal = Vector3.zero;
            if (Carrier != null)
            {
                transform.position = Carrier.Pos;
                frameLocal = new Vector3(0, 1.75f, -0.05f);
            }
            else if (flying)
            {
                flyT += dt / FlyTime;
                float t = Mathf.Clamp01(flyT);
                transform.position = Vector3.Lerp(flyFrom, flyTo, t);
                frameLocal = new Vector3(0, 0.3f + Mathf.Sin(t * Mathf.PI) * 2.2f, 0);
                if (t >= 1)
                {
                    flying = false;
                    Fx.Play("hit-spark", Pos + Vector3.up * 0.3f, scale: 0.6f);
                    Game.I.Shake(0.08f, 0.06f);
                }
            }
            frameT.localPosition = frameLocal;

            shownRadius = Mathf.Lerp(shownRadius, Radius, 1f - Mathf.Exp(-6f * dt));
            float flicker = 1f + Mathf.PerlinNoise(Time.time * 3f, 0.3f) * 0.08f;
            pool.localScale = Vector3.one * (shownRadius * 2f * flicker * 24f / 64f);
            pointLight.range = shownRadius * 2.2f;
            pointLight.transform.localPosition = frameLocal + Vector3.up * 1.2f;
        }
    }
}
