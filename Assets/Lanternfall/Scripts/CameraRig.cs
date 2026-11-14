using UnityEngine;

namespace Lanternfall
{
    /// <summary>Over-the-shoulder third-person camera that follows the party leader. Mouse or right stick orbits it.</summary>
    public sealed class CameraRig
    {
        public readonly Camera Cam;
        public float Yaw, Pitch = 16f;
        float dist = 6.2f, shakeT, shakeAmp;
        Vector3 focus;
        const float Shoulder = 0.65f;

        public CameraRig()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Cam = go.AddComponent<Camera>();
            Cam.fieldOfView = 58f;
            Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 140f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<AudioListener>();
        }

        public void Shake(float t, float amp)
        {
            if (shakeT <= 0) shakeAmp = 0;
            shakeT = Mathf.Max(shakeT, t);
            shakeAmp = Mathf.Max(shakeAmp, amp);
        }

        public void Follow(Hero leader, bool boss, float dt)
        {
            if (leader == null) return;
            if (leader.Input != null)
            {
                var look = leader.Input.Look;
                Yaw += look.x;
                Pitch = Mathf.Clamp(Pitch - look.y, -8f, 60f);
            }
            float wantDist = boss ? 8.5f : 6.2f;
            dist = Mathf.Lerp(dist, wantDist, 1f - Mathf.Exp(-2f * dt));
            var target = leader.Pos + Vector3.up * (leader.Downed ? 0.8f : 1.65f);
            focus = Vector3.Lerp(focus == Vector3.zero ? target : focus, target, 1f - Mathf.Exp(-12f * dt));
            Place(dt);
        }

        /// <summary>Slow orbit around a point (title screen).</summary>
        public void Orbit(Vector3 center, float radius, float height, float dt)
        {
            Yaw += dt * 8f;
            Pitch = 10f; dist = radius;
            focus = center + Vector3.up * height;
            Place(dt);
        }

        void Place(float dt)
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var pos = focus + rot * new Vector3(Shoulder, 0, -dist);
            if (pos.y < 0.3f) pos.y = 0.3f;
            var flat = new Vector2(pos.x, pos.z);
            float lim = Arena.Radius + 0.4f;
            if (flat.magnitude > lim) { flat = flat.normalized * lim; pos.x = flat.x; pos.z = flat.y; }
            if (shakeT > 0)
            {
                shakeT -= dt;
                pos += Random.insideUnitSphere * shakeAmp * 0.6f;
            }
            Cam.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>Where the leader aims: along the camera, snapped to the enemy closest to the crosshair.</summary>
        public Vector3 AimDirection(Vector3 heroPos)
        {
            var fwd = Cam.transform.forward;
            var best = LockTarget();
            if (best != null) return Util.Flat(best.Pos - heroPos).normalized;
            var flatFwd = Util.Flat(fwd);
            return flatFwd.sqrMagnitude > 0.001f ? flatFwd.normalized : Vector3.forward;
        }

        public Enemy LockTarget()
        {
            var fwd = Cam.transform.forward; Enemy best = null; float bestAng = 14f;
            foreach (var e in Game.I.Enemies)
            {
                if (e.Dead) continue;
                var to = e.Pos + Vector3.up * e.Height * 0.5f - Cam.transform.position;
                if (to.magnitude > 26f) continue;
                float ang = Vector3.Angle(fwd, to);
                if (ang < bestAng) { bestAng = ang; best = e; }
            }
            return best;
        }
    }
}
