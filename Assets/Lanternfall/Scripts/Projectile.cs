using UnityEngine;

namespace Lanternfall
{
    public sealed class Projectile : MonoBehaviour
    {
        Hero owner;
        Vector3 dir;
        float speed, damage, range, travelled;
        Element element;
        FxPlayer fx;

        public static void Fire(Hero owner, string fxName, Vector3 from, Vector3 dir, float speed, float damage, float range, Element el)
        {
            var go = new GameObject("proj " + fxName);
            go.transform.position = from;
            var p = go.AddComponent<Projectile>();
            p.owner = owner; p.dir = dir.normalized; p.speed = speed; p.damage = damage; p.range = range; p.element = el;
            float roll = Fx.RollFor(p.dir, out bool flip);
            p.fx = Fx.Play(fxName, from, flip, roll, go.transform, 0.75f, true);
        }

        void Update()
        {
            var g = Game.I;
            float step = speed * Time.deltaTime;
            transform.position += dir * step;
            travelled += step;
            var flat = new Vector3(transform.position.x, 0, transform.position.z);

            foreach (var e in g.Enemies)
            {
                if (e.Dead) continue;
                if ((e.Pos - flat).sqrMagnitude < (e.Radius + 0.35f) * (e.Radius + 0.35f))
                {
                    Fx.Play("hit-spark", transform.position, dir.x < 0, scale: 0.8f);
                    e.TakeHit(new Hit(owner, damage, element, flat - dir));
                    Kill();
                    return;
                }
            }
            if (travelled > range || Mathf.Abs(flat.x) > g.Arena.HalfW || Mathf.Abs(flat.z) > g.Arena.HalfD) Kill();
        }

        void Kill()
        {
            if (fx) fx.Stop();
            Destroy(gameObject);
        }
    }

    /// <summary>A jagged lightning line between two points that lives for a few frames.</summary>
    public sealed class Bolt : MonoBehaviour
    {
        float life;

        public static void Draw(Vector3 a, Vector3 b, Color c, float life = 0.1f, float width = 0.1f)
        {
            var go = new GameObject("bolt");
            var bolt = go.AddComponent<Bolt>();
            bolt.life = life;
            var lr = go.AddComponent<LineRenderer>();
            if (Fx.SpriteMaterial) lr.sharedMaterial = Fx.SpriteMaterial;
            lr.startColor = lr.endColor = c;
            lr.startWidth = width; lr.endWidth = width * 0.6f;
            lr.numCapVertices = 0;
            const int n = 8;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var p = Vector3.Lerp(a, b, t);
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.25f;
                lr.SetPosition(i, p);
            }
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0) Destroy(gameObject);
        }
    }
}
