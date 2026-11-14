using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    /// <summary>
    /// One human player's input. P1 drives the camera.
    ///   Gamepad:  L-stick move · R-stick camera · X/□ or RT attack · Y/△ or RB special · B/○ dodge · LB or LT ultimate · A/✕ revive/confirm · Start pause
    ///   Keyboard + mouse: WASD move · mouse camera · LMB attack · RMB or E special · Space dodge · Q ultimate · F revive · Enter confirm · Esc pause
    ///   Keyboard P2 (couch): arrows move · , attack · . special · / dodge · Right Shift ultimate · Right Ctrl revive
    /// </summary>
    public abstract class Controls
    {
        public abstract string Label { get; }
        public abstract bool Connected { get; }
        public abstract Vector2 Move { get; }
        public abstract Vector2 Look { get; }       // degrees this frame
        public abstract bool Attack { get; }        // held
        public abstract bool Special { get; }       // pressed
        public abstract bool Dodge { get; }
        public abstract bool Ultimate { get; }
        public abstract bool Interact { get; }      // held (revive)
        public abstract bool Confirm { get; }       // pressed (menus)
        public abstract bool AnyJoin { get; }
        public abstract bool UsesMouse { get; }
        protected float lastNavX;

        /// <summary>-1 / +1 once per tilt, for menus.</summary>
        public int NavX()
        {
            float x = Move.x;
            int r = 0;
            if (x > 0.6f && lastNavX <= 0.6f) r = 1;
            if (x < -0.6f && lastNavX >= -0.6f) r = -1;
            lastNavX = x;
            return r;
        }

        public static bool PausePressed()
        {
            foreach (var g in Gamepad.all) if (g.startButton.wasPressedThisFrame) return true;
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }
    }

    public sealed class GamepadControls : Controls
    {
        public readonly Gamepad Pad;
        public GamepadControls(Gamepad pad) { Pad = pad; }
        public override string Label => "Gamepad";
        public override bool Connected => Pad != null && Pad.added;
        public override bool UsesMouse => false;
        static Vector2 Dead(Vector2 v, float d = 0.18f) => v.magnitude < d ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
        public override Vector2 Move => Connected ? Dead(Pad.leftStick.ReadValue() + Pad.dpad.ReadValue()) : Vector2.zero;
        public override Vector2 Look => Connected ? Dead(Pad.rightStick.ReadValue()) * new Vector2(150f, 90f) * Time.unscaledDeltaTime : Vector2.zero;
        public override bool Attack => Connected && (Pad.buttonWest.isPressed || Pad.rightTrigger.isPressed);
        public override bool Special => Connected && (Pad.buttonNorth.wasPressedThisFrame || Pad.rightShoulder.wasPressedThisFrame);
        public override bool Dodge => Connected && Pad.buttonEast.wasPressedThisFrame;
        public override bool Ultimate => Connected && (Pad.leftShoulder.wasPressedThisFrame || Pad.leftTrigger.wasPressedThisFrame);
        public override bool Interact => Connected && Pad.buttonSouth.isPressed;
        public override bool Confirm => Connected && Pad.buttonSouth.wasPressedThisFrame;
        public override bool AnyJoin => Connected && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame);
    }

    public sealed class KeyboardControls : Controls
    {
        public readonly int Layout; // 0 = WASD + mouse, 1 = arrows
        public KeyboardControls(int layout) { Layout = layout; }
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        public override string Label => Layout == 0 ? "Keyboard + Mouse" : "Keyboard (arrows)";
        public override bool Connected => K != null;
        public override bool UsesMouse => Layout == 0;
        bool Held(Key k) => K != null && K[k].isPressed;
        bool Pressed(Key k) => K != null && K[k].wasPressedThisFrame;

        public override Vector2 Move
        {
            get
            {
                var v = Layout == 0
                    ? new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0))
                    : new Vector2((Held(Key.RightArrow) ? 1 : 0) - (Held(Key.LeftArrow) ? 1 : 0), (Held(Key.UpArrow) ? 1 : 0) - (Held(Key.DownArrow) ? 1 : 0));
                return v.sqrMagnitude > 1 ? v.normalized : v;
            }
        }
        public override Vector2 Look
        {
            get
            {
                if (Layout != 0 || M == null || Cursor.lockState != CursorLockMode.Locked) return Vector2.zero;
                return M.delta.ReadValue() * 0.12f;
            }
        }
        public override bool Attack => Layout == 0 ? (M != null && M.leftButton.isPressed) || Held(Key.J) : Held(Key.Comma);
        public override bool Special => Layout == 0 ? (M != null && M.rightButton.wasPressedThisFrame) || Pressed(Key.E) || Pressed(Key.K) : Pressed(Key.Period);
        public override bool Dodge => Layout == 0 ? Pressed(Key.Space) : Pressed(Key.Slash);
        public override bool Ultimate => Layout == 0 ? Pressed(Key.Q) : Pressed(Key.RightShift);
        public override bool Interact => Layout == 0 ? Held(Key.F) : Held(Key.RightCtrl);
        public override bool Confirm => Layout == 0 ? Pressed(Key.Enter) || Pressed(Key.Space) || Pressed(Key.NumpadEnter) : Pressed(Key.Slash);
        public override bool AnyJoin => Layout == 0
            ? Pressed(Key.Enter) || Pressed(Key.Space) || (M != null && M.leftButton.wasPressedThisFrame)
            : Pressed(Key.Comma) || Pressed(Key.Slash);
    }
}
