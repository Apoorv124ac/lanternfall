using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    /// <summary>
    /// High top-down follow camera (like the devlog reel). Fixed yaw so WASD always means screen-up/down/left/right.
    /// Tab / Select toggles between the close "action" view and a higher "tactical" view.
    /// The camera leans a little toward where the leader is aiming so you see more of what you shoot at.
    /// </summary>
    public sealed class CameraRig
    {
        public readonly Camera Cam;
        public float Yaw, Pitch = 62f;
        public bool Tactical;
        float dist = 15f, shakeT, shakeAmp;
        Vector3 focus;

        public string ModeName => Tactical ? "Tactical" : "Action";

        public CameraRig()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Cam = go.AddComponent<Camera>();
            Cam.fieldOfView = 45f;
            Cam.nearClipPlane = 0.3f; Cam.farClipPlane = 160f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<AudioListener>();
        }

        public void Toggle() => Tactical = !Tactical;

        public void Shake(float t, float amp)
        {
            if (shakeT <= 0) shakeAmp = 0;
            shakeT = Mathf.Max(shakeT, t);
            shakeAmp = Mathf.Max(shakeAmp, amp);
        }

        public void Follow(Hero leader, bool boss, float dt)
        {
            if (leader == null) return;
            Yaw = 0f;
            float wantPitch = Tactical ? 72f : 60f;
            float wantDist = (Tactical ? 22f : 15f) + (boss ? 3.5f : 0f);
            Pitch = Mathf.Lerp(Pitch, wantPitch, 1f - Mathf.Exp(-5f * dt));
            dist = Mathf.Lerp(dist, wantDist, 1f - Mathf.Exp(-4f * dt));

            var target = leader.Pos + Vector3.up * 0.9f;
            if (!leader.Downed)
            {
                var lead = Util.Flat(leader.AimPoint - leader.Pos);
                float maxLead = leader.Ads ? 4.5f : 2.2f;
                if (lead.magnitude > maxLead) lead = lead.normalized * maxLead;
                target += lead * 0.6f;
            }
            focus = Vector3.Lerp(focus == Vector3.zero ? target : focus, target, 1f - Mathf.Exp(-7f * dt));
            Place(dt);
        }

        /// <summary>Slow orbit around a point (title screen).</summary>
        public void Orbit(Vector3 center, float radius, float height, float dt)
        {
            Yaw += dt * 8f;
            Pitch = 28f; dist = radius;
            focus = center + Vector3.up * height;
            Place(dt);
        }

        void Place(float dt)
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            var pos = focus + rot * new Vector3(0, 0, -dist);
            if (shakeT > 0)
            {
                shakeT -= dt;
                pos += Random.insideUnitSphere * shakeAmp * 0.6f;
            }
            Cam.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>The point on the horizontal plane at height y under the mouse cursor.</summary>
        public Vector3 MouseGround(float y)
        {
            var mouse = Mouse.current;
            Vector2 sp = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return ScreenGround(sp, y);
        }

        public Vector3 ScreenGround(Vector2 screen, float y)
        {
            var ray = Cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            var plane = new Plane(Vector3.up, new Vector3(0, y, 0));
            if (plane.Raycast(ray, out float t)) return ray.GetPoint(t);
            return focus;
        }
    }
}
