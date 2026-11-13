using UnityEngine;

namespace Lanternfall
{
    /// <summary>Anything that walks the arena floor: a billboard sprite, a blob shadow, knockback and lantern-light shading.</summary>
    public abstract class Actor : MonoBehaviour
    {
        public float Radius = 0.45f;
        public float Height = 1.6f;          // where status effects sit
        protected SpriteRenderer body;
        protected Transform bodyT;
        protected Sprite[] frames;
        protected float animT, flashT, hover;
        protected Vector3 knock;
        protected bool facingLeft;

        public Vector3 Pos => new Vector3(transform.position.x, 0, transform.position.z);

        protected void BuildVisual(Sprite[] sprites, float scale = 1f, float shadowSize = 1.1f)
        {
            frames = sprites;
            var shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArt.Blob(new Color(0, 0, 0, 0.55f), 24);
            if (Fx.SpriteMaterial) shadow.sharedMaterial = Fx.SpriteMaterial;
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3(0, 0.02f, 0);
            shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shadow.transform.localScale = new Vector3(shadowSize, shadowSize * 0.6f, 1) * scale;
            shadow.sortingOrder = -5;

            body = new GameObject("body").AddComponent<SpriteRenderer>();
            if (Fx.SpriteMaterial) body.sharedMaterial = Fx.SpriteMaterial;
            bodyT = body.transform;
            bodyT.SetParent(transform, false);
            bodyT.localScale = Vector3.one * scale;
            body.sprite = frames[0];
            body.sortingOrder = 5;
        }

        protected void AnimateBody(bool moving, float rate = 0.14f, float roll = 0f)
        {
            if (moving) animT += Time.deltaTime; else animT = 0;
            int f = moving ? 1 - (int)(animT / rate) % 2 : 0;
            if (frames.Length > 1) body.sprite = frames[Mathf.Min(f, frames.Length - 1)];
            body.flipX = facingLeft;
            bodyT.localPosition = new Vector3(0, hover + (moving && f == 1 ? 0.04f : 0), 0);
            Billboard.Face(bodyT, roll);

            // Lantern light: things in the dark fade to silhouettes.
            float lit = Game.I && Game.I.Lantern ? Game.I.Lantern.LightAt(Pos) : 1f;
            var c = Color.Lerp(new Color(0.22f, 0.2f, 0.34f), Color.white, lit);
            if (flashT > 0) { flashT -= Time.deltaTime; c = new Color(1f, 0.35f, 0.35f); }
            body.color = TintExtra(c);
        }

        protected virtual Color TintExtra(Color c) => c;

        public void Push(Vector3 dir, float force) { dir.y = 0; knock += dir.normalized * force; }

        /// <summary>Applies knockback and keeps the actor inside the arena and out of pillars.</summary>
        protected void Move(Vector3 delta)
        {
            knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * Time.deltaTime);
            var p = transform.position + delta + knock * Time.deltaTime;
            p = Game.I ? Game.I.Arena.Resolve(p, Radius) : p;
            p.y = 0;
            transform.position = p;
        }
    }
}
