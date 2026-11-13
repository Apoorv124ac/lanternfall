using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    /// <summary>A pixelfx sprite sheet cut into Unity sprites.</summary>
    public sealed class FxClip
    {
        public string Name;
        public FxInfo Info;
        public Sprite[] Frames;
        public float Duration => Info.Frames / (float)Info.Fps;
    }

    /// <summary>Loads pixelfx sheets from Resources/FX and plays them as camera-facing billboards.</summary>
    public static class Fx
    {
        /// <summary>World units per pixelfx pixel. Heroes are 24x38 px, so they stand about 1.6 units tall.</summary>
        public const float PixelsPerUnit = 24f;

        static readonly Dictionary<string, FxClip> cache = new Dictionary<string, FxClip>();
        static Material spriteMaterial;

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    spriteMaterial = shader != null ? new Material(shader) : null;
                }
                return spriteMaterial;
            }
        }

        public static FxClip Get(string name)
        {
            if (cache.TryGetValue(name, out var clip)) return clip;
            if (!FxData.All.TryGetValue(name, out var info))
            {
                Debug.LogError($"Lanternfall: no FxData entry for '{name}'");
                return null;
            }
            var tex = Resources.Load<Texture2D>("FX/" + name);
            if (tex == null)
            {
                Debug.LogError($"Lanternfall: missing Resources/FX/{name}.png");
                return null;
            }
            tex.filterMode = FilterMode.Point;
            var frames = new Sprite[info.Frames];
            var pivot = new Vector2(info.PivotX / (float)info.W, 1f - info.PivotY / (float)info.H);
            for (int i = 0; i < info.Frames; i++)
            {
                int col = i % info.Columns, row = i / info.Columns;
                var rect = new Rect(col * info.W, tex.height - (row + 1) * info.H, info.W, info.H);
                frames[i] = Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{name} {i}";
            }
            clip = new FxClip { Name = name, Info = info, Frames = frames };
            cache[name] = clip;
            return clip;
        }

        /// <summary>When the effect's first hit lands (pixelfx act timing), so damage syncs with the visual.</summary>
        public static float ContactTime(string name, float fallback = 0.3f)
        {
            var c = Get(name);
            return c != null && c.Info.Contact > 0 ? c.Info.Contact : fallback;
        }

        /// <summary>Plays an effect at a world position. Directional effects face +X; flip mirrors them, roll turns them on screen.</summary>
        public static FxPlayer Play(string name, Vector3 pos, bool flip = false, float roll = 0f,
            Transform follow = null, float scale = 1f, bool loop = false, float speed = 1f)
        {
            var clip = Get(name);
            if (clip == null) return null;
            var go = new GameObject("fx " + name);
            go.transform.position = pos;
            var p = go.AddComponent<FxPlayer>();
            p.Init(clip, flip, roll, follow, scale, loop || clip.Info.Loop, speed);
            return p;
        }

        /// <summary>Screen-space roll (degrees) for an effect aimed along a ground direction.</summary>
        public static float RollFor(Vector3 groundDir, out bool flip)
        {
            var cam = Camera.main;
            Vector3 right = cam ? cam.transform.right : Vector3.right;
            Vector3 up = cam ? Vector3.ProjectOnPlane(cam.transform.up, Vector3.up).normalized : Vector3.forward;
            float x = Vector3.Dot(groundDir, right), y = Vector3.Dot(groundDir, up) * 0.6f; // squash depth: camera is tilted
            flip = x < 0f;
            float a = Mathf.Atan2(y, Mathf.Abs(x)) * Mathf.Rad2Deg;
            return flip ? -a : a;
        }
    }

    public sealed class FxPlayer : MonoBehaviour
    {
        FxClip clip;
        SpriteRenderer sr;
        Transform follow;
        Vector3 followOffset;
        float t, speed, roll;
        bool loop, following;
        public float Age => t;

        public void Init(FxClip c, bool flip, float rollDeg, Transform followTarget, float scale, bool looping, float spd)
        {
            clip = c; loop = looping; speed = spd; roll = rollDeg; follow = followTarget;
            following = follow != null;
            if (following) followOffset = transform.position - follow.position;
            sr = gameObject.AddComponent<SpriteRenderer>();
            if (Fx.SpriteMaterial) sr.sharedMaterial = Fx.SpriteMaterial;
            sr.sprite = c.Frames[0];
            sr.flipX = flip;
            sr.sortingOrder = 10;
            transform.localScale = Vector3.one * scale;
            Billboard.Face(transform, roll);
        }

        public void Stop() => Destroy(gameObject);

        void LateUpdate()
        {
            if (follow) transform.position = follow.position + followOffset;
            else if (following) { Destroy(gameObject); return; } // target died
            t += Time.deltaTime * speed;
            int f = Mathf.FloorToInt(t * clip.Info.Fps);
            if (f >= clip.Info.Frames)
            {
                if (!loop) { Destroy(gameObject); return; }
                f %= clip.Info.Frames;
            }
            sr.sprite = clip.Frames[f];
            Billboard.Face(transform, roll);
        }
    }

    /// <summary>Turns a transform to face the camera (a cylindrical billboard looks wrong for a tilted top-down camera, so we copy its rotation).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate() => Face(transform, 0f);

        public static void Face(Transform t, float roll)
        {
            var cam = Camera.main;
            if (!cam) return;
            t.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, roll);
        }
    }
}
