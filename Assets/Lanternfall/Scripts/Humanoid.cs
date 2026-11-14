using System.Collections.Generic;
using UnityEngine;

namespace Lanternfall
{
    public enum Weapon { None, Staff, Orb, Sword, Bow, Club, Shield, Rods, Gauntlet }
    public enum Pose { None, Cast, Swing, Special, Bow, Hit, Dodge, Ultimate, Slam }

    /// <summary>How a character looks. Heroes and the Eclipse cult share one body, dressed differently.</summary>
    public sealed class Look
    {
        public Color Skin = Util.Hex("#d9a184"), Cloth = Color.gray, Cloth2 = Color.gray, Trim = Color.white, Glow = Color.white, Hair = Util.Hex("#2a1d18"), Eyes = Util.Hex("#1b1424");
        public bool Robe = true, Hood, Mask, Halo, SpikyHair, Pauldrons, Crown, Goggles, Cape, Horns, Ponytail;
        public Weapon Right = Weapon.None, Left = Weapon.None;
        public float Height = 1f, Bulk = 1f;
    }

    /// <summary>
    /// A procedurally modelled and animated humanoid (about 1.8 m tall, facing +Z).
    /// Built from primitives plus generated cones and rings; animated in code (walk cycle, casts, swings, dodge roll, knockdown).
    /// Swap it for a rigged model later via CharacterArt; the gameplay code only calls Animate().
    /// </summary>
    public sealed class Humanoid : MonoBehaviour
    {
        Transform rig, hips, spine, head, armL, armR, foreL, foreR, handL, handR, legL, legR, shinL, shinR, cape;
        readonly List<Renderer> renderers = new List<Renderer>();
        MaterialPropertyBlock block;
        float phase, breathe, lie;
        Look look;
        public Transform HandR => handR;
        public Transform HandL => handL;
        public Transform Head => head;
        public float Scale => look.Height;

        Animator animator;
        bool hasSpeed, hasMotion, hasGrounded;

        /// <summary>Uses an imported rigged model (e.g. Unity Starter Assets or a Mixamo character) instead of the built-in body.</summary>
        public static Humanoid FromModel(Transform parent, GameObject prefab, Look l)
        {
            var root = new GameObject("Body");
            root.transform.SetParent(parent, false);
            var h = root.AddComponent<Humanoid>();
            h.look = l;
            h.rig = h.Pivot("Rig", root.transform, Vector3.zero);
            var model = Instantiate(prefab, h.rig);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            h.rig.localScale = Vector3.one * l.Height;
            // strip the asset's own controllers so only our gameplay code drives it
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var cc in model.GetComponentsInChildren<CharacterController>(true)) Destroy(cc);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            h.animator = model.GetComponentInChildren<Animator>();
            if (h.animator)
            {
                h.animator.applyRootMotion = false;
                foreach (var p in h.animator.parameters)
                {
                    if (p.name == "Speed") h.hasSpeed = true;
                    if (p.name == "MotionSpeed") h.hasMotion = true;
                    if (p.name == "Grounded") h.hasGrounded = true;
                }
                if (h.animator.isHuman)
                {
                    h.handR = h.animator.GetBoneTransform(HumanBodyBones.RightHand);
                    h.handL = h.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    h.head = h.animator.GetBoneTransform(HumanBodyBones.Head);
                }
            }
            h.GetComponentsInChildren(h.renderers);
            h.block = new MaterialPropertyBlock();
            return h;
        }

        public static Humanoid Build(Transform parent, Look l)
        {
            var root = new GameObject("Body");
            root.transform.SetParent(parent, false);
            var h = root.AddComponent<Humanoid>();
            h.look = l;
            h.Construct();
            return h;
        }

        Transform Pivot(string n, Transform p, Vector3 pos)
        {
            var t = new GameObject(n).transform;
            t.SetParent(p, false);
            t.localPosition = pos;
            return t;
        }

        // A limb segment hanging down from its pivot.
        void Limb(Transform pivot, float len, float thick, Material m)
        {
            Util.Prim(PrimitiveType.Capsule, pivot, new Vector3(0, -len / 2, 0), new Vector3(thick, len / 2 + thick / 4, thick), m);
        }

        void Construct()
        {
            var L = look;
            float b = L.Bulk;
            Material skin = Util.Lit(L.Skin), cloth = Util.Lit(L.Cloth), cloth2 = Util.Lit(L.Cloth2), trim = Util.Lit(L.Trim), glow = Util.Glow(L.Glow), hair = Util.Lit(L.Hair), dark = Util.Lit(Util.Hex("#1a1520"));

            rig = Pivot("Rig", transform, Vector3.zero);
            rig.localScale = Vector3.one * L.Height;
            hips = Pivot("Hips", rig, new Vector3(0, 0.95f, 0));
            Util.Prim(PrimitiveType.Cube, hips, new Vector3(0, 0.02f, 0), new Vector3(0.34f * b, 0.18f, 0.22f * b), cloth2);
            Util.MeshObj("Belt", Util.Frustum(0.2f * b, 0.2f * b, 0.06f), hips, new Vector3(0, 0.06f, 0), trim);

            // legs
            legL = Pivot("LegL", hips, new Vector3(-0.1f * b, -0.03f, 0));
            legR = Pivot("LegR", hips, new Vector3(0.1f * b, -0.03f, 0));
            foreach (var leg in new[] { legL, legR })
            {
                Limb(leg, 0.45f, 0.15f * b, cloth2);
                var shin = Pivot("Shin", leg, new Vector3(0, -0.45f, 0));
                Limb(shin, 0.43f, 0.13f * b, dark);
                Util.Prim(PrimitiveType.Cube, shin, new Vector3(0, -0.45f, 0.05f), new Vector3(0.12f * b, 0.08f, 0.25f), dark);
                if (leg == legL) shinL = shin; else shinR = shin;
            }
            if (L.Robe)
                Util.MeshObj("Robe", Util.Frustum(0.36f * b, 0.2f * b, 0.72f), hips, new Vector3(0, -0.74f, 0), cloth);

            // torso
            spine = Pivot("Spine", hips, new Vector3(0, 0.08f, 0));
            Util.Prim(PrimitiveType.Capsule, spine, new Vector3(0, 0.3f, 0), new Vector3(0.42f * b, 0.3f, 0.27f * b), cloth);
            Util.Prim(PrimitiveType.Cube, spine, new Vector3(0, 0.3f, 0.11f * b), new Vector3(0.08f, 0.42f, 0.04f), trim);     // tunic seam
            if (L.Pauldrons)
            {
                Util.Prim(PrimitiveType.Sphere, spine, new Vector3(-0.27f * b, 0.53f, 0), new Vector3(0.24f, 0.16f, 0.24f) * b, trim);
                Util.Prim(PrimitiveType.Sphere, spine, new Vector3(0.27f * b, 0.53f, 0), new Vector3(0.24f, 0.16f, 0.24f) * b, trim);
            }
            if (L.Cape)
            {
                cape = Pivot("Cape", spine, new Vector3(0, 0.56f, -0.13f * b));
                Util.Prim(PrimitiveType.Cube, cape, new Vector3(0, -0.45f, 0), new Vector3(0.46f * b, 0.95f, 0.03f), cloth2);
            }

            // head
            head = Pivot("Head", spine, new Vector3(0, 0.64f, 0));
            Util.Prim(PrimitiveType.Capsule, head, new Vector3(0, -0.04f, 0), new Vector3(0.1f, 0.06f, 0.1f), skin); // neck
            Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.13f, 0.01f), new Vector3(0.24f, 0.27f, 0.25f), skin);
            if (L.Mask)
            {
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.13f, 0.04f), new Vector3(0.25f, 0.26f, 0.24f), dark);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(-0.055f, 0.15f, 0.15f), Vector3.one * 0.045f, glow);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0.055f, 0.15f, 0.15f), Vector3.one * 0.045f, glow);
            }
            else
            {
                var eyes = Util.Lit(L.Eyes);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(-0.05f, 0.15f, 0.12f), Vector3.one * 0.035f, eyes);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0.05f, 0.15f, 0.12f), Vector3.one * 0.035f, eyes);
                Util.Prim(PrimitiveType.Cube, head, new Vector3(-0.05f, 0.19f, 0.12f), new Vector3(0.05f, 0.012f, 0.01f), hair); // brows
                Util.Prim(PrimitiveType.Cube, head, new Vector3(0.05f, 0.19f, 0.12f), new Vector3(0.05f, 0.012f, 0.01f), hair);
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.12f, 0.13f), new Vector3(0.03f, 0.04f, 0.03f), skin); // nose
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.18f, -0.01f), new Vector3(0.255f, 0.24f, 0.26f), hair); // hair cap
            }
            if (L.Hood)
            {
                Util.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.16f, -0.025f), new Vector3(0.31f, 0.33f, 0.31f), cloth);
                Util.MeshObj("HoodTip", Util.Frustum(0.12f, 0.0f, 0.22f, 8), head, new Vector3(0, 0.22f, -0.12f), cloth, new Vector3(-55, 0, 0));
            }
            if (L.SpikyHair && !L.Hood)
                for (int i = 0; i < 5; i++)
                    Util.MeshObj("Spike", Util.Frustum(0.06f, 0f, 0.18f, 6), head, new Vector3((i - 2) * 0.05f, 0.27f, -0.02f - Mathf.Abs(i - 2) * 0.02f), hair, new Vector3(-25, 0, (i - 2) * -18));
            if (L.Ponytail && !L.Hood)
                Util.Prim(PrimitiveType.Capsule, head, new Vector3(0, 0.08f, -0.17f), new Vector3(0.08f, 0.14f, 0.08f), hair, new Vector3(25, 0, 0));
            if (L.Goggles)
            {
                Util.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.15f, 0.06f), new Vector3(0.27f, 0.05f, 0.2f), dark);
                Util.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.15f, 0.125f), new Vector3(0.2f, 0.045f, 0.02f), glow);
            }
            if (L.Halo)
                Util.MeshObj("Halo", Util.Ring(0.16f, 0.2f), head, new Vector3(0, 0.42f, -0.02f), glow, new Vector3(-12, 0, 0));
            if (L.Crown)
                for (int i = 0; i < 7; i++)
                {
                    float a = i / 7f * Mathf.PI * 2;
                    Util.MeshObj("Crown", Util.Frustum(0.035f, 0f, 0.2f, 5), head, new Vector3(Mathf.Sin(a) * 0.13f, 0.27f, Mathf.Cos(a) * 0.13f), glow, new Vector3(Mathf.Cos(a) * -18, 0, Mathf.Sin(a) * 18));
                }
            if (L.Horns)
            {
                Util.MeshObj("Horn", Util.Frustum(0.05f, 0f, 0.26f, 6), head, new Vector3(-0.11f, 0.24f, 0), dark, new Vector3(0, 0, 35));
                Util.MeshObj("Horn", Util.Frustum(0.05f, 0f, 0.26f, 6), head, new Vector3(0.11f, 0.24f, 0), dark, new Vector3(0, 0, -35));
            }

            // arms
            armL = Pivot("ArmL", spine, new Vector3(-0.27f * b, 0.52f, 0));
            armR = Pivot("ArmR", spine, new Vector3(0.27f * b, 0.52f, 0));
            foreach (var arm in new[] { armL, armR })
            {
                Limb(arm, 0.3f, 0.12f * b, cloth);
                var fore = Pivot("Forearm", arm, new Vector3(0, -0.3f, 0));
                Limb(fore, 0.27f, 0.105f * b, L.Right == Weapon.Gauntlet ? trim : skin);
                Util.Prim(PrimitiveType.Cube, fore, new Vector3(0, -0.12f, 0), new Vector3(0.12f, 0.08f, 0.12f) * b, trim); // bracer
                var hand = Pivot("Hand", fore, new Vector3(0, -0.3f, 0));
                Util.Prim(PrimitiveType.Sphere, hand, Vector3.zero, Vector3.one * 0.1f * b, skin);
                if (arm == armL) { foreL = fore; handL = hand; } else { foreR = fore; handR = hand; }
            }
            AddWeapon(handR, L.Right, glow, trim, dark);
            AddWeapon(handL, L.Left, glow, trim, dark);

            GetComponentsInChildren(renderers);
            block = new MaterialPropertyBlock();
        }

        void AddWeapon(Transform hand, Weapon w, Material glow, Material trim, Material dark)
        {
            var wood = Util.Lit(Util.Hex("#4a3426"));
            switch (w)
            {
                case Weapon.Staff:
                    Util.Prim(PrimitiveType.Cylinder, hand, new Vector3(0, 0.15f, 0.04f), new Vector3(0.045f, 0.8f, 0.045f), wood);
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, 0.98f, 0.04f), Vector3.one * 0.16f, glow);
                    Util.MeshObj("Prong", Util.Ring(0.09f, 0.12f, 12), hand, new Vector3(0, 0.98f, 0.04f), trim, new Vector3(90, 0, 0));
                    break;
                case Weapon.Orb:
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0.14f), Vector3.one * 0.17f, glow);
                    break;
                case Weapon.Sword:
                    Util.Prim(PrimitiveType.Cube, hand, new Vector3(0, -0.02f, 0.4f), new Vector3(0.05f, 0.02f, 0.72f), Util.Lit(Util.Hex("#9aa0b5")));
                    Util.Prim(PrimitiveType.Cube, hand, new Vector3(0, -0.02f, 0.05f), new Vector3(0.18f, 0.04f, 0.04f), trim);
                    break;
                case Weapon.Club:
                    Util.Prim(PrimitiveType.Cylinder, hand, new Vector3(0, -0.02f, 0.35f), new Vector3(0.09f, 0.35f, 0.09f), wood, new Vector3(90, 0, 0));
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0.7f), Vector3.one * 0.24f, dark);
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0.7f), Vector3.one * 0.12f, glow);
                    break;
                case Weapon.Bow:
                    for (int i = -2; i <= 2; i++)
                        Util.Prim(PrimitiveType.Cube, hand, new Vector3(0, i * 0.17f, 0.06f + Mathf.Abs(i) * -0.05f), new Vector3(0.035f, 0.19f, 0.035f), wood, new Vector3(i * 14f, 0, 0));
                    Util.Prim(PrimitiveType.Cube, hand, new Vector3(0, 0, -0.08f), new Vector3(0.008f, 0.8f, 0.008f), glow);
                    break;
                case Weapon.Shield:
                    Util.Prim(PrimitiveType.Cube, hand, new Vector3(0.08f, 0.05f, 0.05f), new Vector3(0.05f, 0.55f, 0.42f), trim, new Vector3(0, 0, 0));
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0.11f, 0.05f, 0.05f), new Vector3(0.03f, 0.14f, 0.14f), glow);
                    break;
                case Weapon.Rods:
                    Util.Prim(PrimitiveType.Cylinder, hand, new Vector3(0, -0.02f, 0.18f), new Vector3(0.035f, 0.2f, 0.035f), dark, new Vector3(90, 0, 0));
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0.4f), Vector3.one * 0.07f, glow);
                    break;
                case Weapon.Gauntlet:
                    Util.Prim(PrimitiveType.Sphere, hand, Vector3.zero, Vector3.one * 0.15f, trim);
                    Util.Prim(PrimitiveType.Sphere, hand, new Vector3(0, -0.02f, 0.08f), Vector3.one * 0.09f, glow);
                    break;
            }
        }

        /// <summary>Tints every part (hit flash, frozen, elite glow). Pass Color.clear to reset.</summary>
        Color lastTint = Color.clear;
        public void Tint(Color c)
        {
            if (c == lastTint) return;
            lastTint = c;
            if (c.a <= 0) { foreach (var r in renderers) if (r) r.SetPropertyBlock(null); return; }
            block.Clear();
            block.SetColor(Util.ColorId, c);
            block.SetColor(Util.BaseColorId, c);
            foreach (var r in renderers) if (r) r.SetPropertyBlock(block);
        }

        /// <param name="speed">ground speed in m/s</param>
        /// <param name="pose">action overriding the upper body</param>
        /// <param name="t">0..1 progress through the action</param>
        /// <param name="down">0 standing .. 1 lying on the floor</param>
        public void Animate(float dt, float speed, Pose pose, float t, float down)
        {
            if (animator != null || hips == null)
            {
                if (animator)
                {
                    if (hasSpeed) animator.SetFloat("Speed", speed);
                    if (hasMotion) animator.SetFloat("MotionSpeed", 1f);
                    if (hasGrounded) animator.SetBool("Grounded", true);
                    animator.speed = dt > 0 ? 1f : 0f;
                }
                float roll = pose == Pose.Dodge ? 360f * Mathf.SmoothStep(0, 1, t) : 0f;
                lie = Mathf.MoveTowards(lie, down, dt * 4);
                rig.localRotation = Quaternion.Euler(roll - 88 * lie, 0, 0);
                rig.localPosition = new Vector3(0, (pose == Pose.Dodge ? 0.5f * Mathf.Sin(t * Mathf.PI) : 0) + 0.15f * lie, 0.8f * lie * look.Height);
                return;
            }
            phase += dt * Mathf.Max(speed, 0.0f) * 1.7f;
            breathe += dt;
            float amt = Mathf.Clamp01(speed / 4.5f);
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

            // legs
            legL.localRotation = Quaternion.Euler(-s * 42 * amt, 0, 0);
            legR.localRotation = Quaternion.Euler(s * 42 * amt, 0, 0);
            shinL.localRotation = Quaternion.Euler(Mathf.Max(0, -c) * 55 * amt + 4, 0, 0);
            shinR.localRotation = Quaternion.Euler(Mathf.Max(0, c) * 55 * amt + 4, 0, 0);

            // body
            float bob = Mathf.Abs(c) * 0.05f * amt;
            hips.localPosition = new Vector3(0, 0.95f - 0.03f * amt + bob, 0);
            hips.localRotation = Quaternion.Euler(0, s * 8 * amt, 0);
            float lean = 9 * amt + Mathf.Sin(breathe * 1.8f) * 1.2f;
            float twist = -s * 10 * amt;

            // arms: walk swing by default
            Vector3 aL = new Vector3(s * 35 * amt, 0, -8), aR = new Vector3(-s * 35 * amt, 0, 8);
            Vector3 fL = new Vector3(-20 - 20 * amt, 0, 0), fR = fL;

            float w = t < 0.25f ? t / 0.25f : 1f - Mathf.Clamp01((t - 0.6f) / 0.4f); // rise, hold, fall
            switch (pose)
            {
                case Pose.Cast:
                    aR = Vector3.Lerp(aR, new Vector3(-88, 0, 4), w); fR = Vector3.Lerp(fR, new Vector3(-6, 0, 0), w);
                    aL = Vector3.Lerp(aL, new Vector3(-30, 0, -12), w * 0.6f);
                    twist += 14 * w;
                    break;
                case Pose.Swing:
                {
                    float up = t < 0.55f ? t / 0.55f : 0, strike = t >= 0.55f ? Mathf.Clamp01((t - 0.55f) / 0.12f) : 0, rec = t > 0.75f ? (t - 0.75f) / 0.25f : 0;
                    var raised = new Vector3(-155, 0, 15); var down2 = new Vector3(-25, 0, 10);
                    aR = t < 0.55f ? Vector3.Lerp(aR, raised, up) : Vector3.Lerp(Vector3.Lerp(raised, down2, strike), aR, rec);
                    fR = new Vector3(-30, 0, 0);
                    twist += t < 0.55f ? -20 * up : Mathf.Lerp(25, 0, rec);
                    lean += t < 0.55f ? -6 * up : 12 * (1 - rec);
                    break;
                }
                case Pose.Slam:
                {
                    float up = t < 0.6f ? t / 0.6f : 0, rec = t > 0.7f ? (t - 0.7f) / 0.3f : 0;
                    var raised = new Vector3(-170, 0, 0); var down2 = new Vector3(-45, 0, 0);
                    var arm = t < 0.6f ? Vector3.Lerp(aR, raised, up) : Vector3.Lerp(down2, aR, rec);
                    aR = arm + new Vector3(0, 0, 10); aL = arm + new Vector3(0, 0, -10);
                    fR = fL = new Vector3(-15, 0, 0);
                    lean += t < 0.6f ? -12 * up : 25 * (1 - rec);
                    break;
                }
                case Pose.Special:
                case Pose.Ultimate:
                {
                    var up2 = new Vector3(pose == Pose.Ultimate ? -175 : -150, 0, 0);
                    aR = Vector3.Lerp(aR, up2 + new Vector3(0, 0, 20), w); aL = Vector3.Lerp(aL, up2 + new Vector3(0, 0, -20), w);
                    fR = fL = Vector3.Lerp(fR, new Vector3(-10, 0, 0), w);
                    lean -= 10 * w;
                    break;
                }
                case Pose.Bow:
                    aL = Vector3.Lerp(aL, new Vector3(-90, 0, -4), w); fL = Vector3.Lerp(fL, Vector3.zero, w);
                    aR = Vector3.Lerp(aR, new Vector3(-85, 0, 30), w); fR = Vector3.Lerp(fR, new Vector3(-120 * Mathf.Clamp01(t * 1.6f), 0, 0), w);
                    twist += 20 * w;
                    break;
                case Pose.Hit:
                    lean -= 18 * (1 - t);
                    aL += new Vector3(0, 0, -25 * (1 - t)); aR += new Vector3(0, 0, 25 * (1 - t));
                    break;
            }
            armL.localRotation = Quaternion.Euler(aL);
            armR.localRotation = Quaternion.Euler(aR);
            foreL.localRotation = Quaternion.Euler(fL);
            foreR.localRotation = Quaternion.Euler(fR);
            spine.localRotation = Quaternion.Euler(lean, twist, 0);
            head.localRotation = Quaternion.Euler(-lean * 0.5f, -twist * 0.6f, 0);
            if (cape) cape.localRotation = Quaternion.Euler(-12 - 30 * amt - Mathf.Sin(breathe * 3) * 3, 0, 0);

            // dodge roll: tuck and spin forward
            if (pose == Pose.Dodge)
            {
                float a = Mathf.SmoothStep(0, 1, t);
                rig.localRotation = Quaternion.Euler(360 * a, 0, 0);
                rig.localPosition = new Vector3(0, 0.55f * Mathf.Sin(a * Mathf.PI) * look.Height, 0);
                legL.localRotation = legR.localRotation = Quaternion.Euler(-80 * Mathf.Sin(a * Mathf.PI), 0, 0);
                return;
            }

            // knocked down: lie on the back
            lie = Mathf.MoveTowards(lie, down, dt * 4);
            rig.localRotation = Quaternion.Euler(-88 * lie, 0, 0);
            rig.localPosition = new Vector3(0, 0.15f * lie, 0.8f * lie * look.Height);
        }
    }
}
