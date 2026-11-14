using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    /// <summary>
    /// One human player's input (twin-stick shooter layout).
    ///   Keyboard + mouse: WASD move · mouse aim · LMB fire · RMB aim down sights · Shift run (double-tap: tactical sprint)
    ///                     Ctrl crouch / slide while running · R reload · E special · Q ultimate · F revive · Tab camera · Esc pause · F3 dev HUD
    ///   Gamepad: L-stick move · R-stick aim · RT fire · LT aim · L3 run (double-click: sprint) · B crouch/slide · X reload
    ///            Y special · RB ultimate · A revive/confirm · Select camera · Start pause
    /// </summary>
    public abstract class Controls
    {
        public abstract string Label { get; }
        public abstract bool UsesMouse { get; }
        public abstract Vector2 Move { get; }
        /// <summary>Gamepad aim stick (zero when idle). Mouse players aim at the cursor instead.</summary>
        public abstract Vector2 AimStick { get; }
        public abstract bool Fire { get; }
        public abstract bool AimDownSights { get; }
        public abstract bool Run { get; }
        public abstract bool RunPressed { get; }
        public abstract bool Crouch { get; }       // pressed
        public abstract bool Reload { get; }
        public abstract bool Special { get; }
        public abstract bool Ultimate { get; }
        public abstract bool Interact { get; }     // held
        public abstract bool Confirm { get; }
        public abstract bool AnyJoin { get; }
        public abstract bool CameraToggle { get; }

        float lastNavX;
        public int NavX()
        {
            float x = Move.x;
            int r = 0;
            if (x > 0.6f && lastNavX <= 0.6f) r = 1;
            if (x < -0.6f && lastNavX >= -0.6f) r = -1;
            lastNavX = x;
            return r;
        }

        // double-tap run → tactical sprint
        float lastRunTap = -9f;
        public bool SprintTap()
        {
            if (!RunPressed) return false;
            bool dbl = Time.unscaledTime - lastRunTap < 0.3f;
            lastRunTap = Time.unscaledTime;
            return dbl;
        }

        public static bool PausePressed()
        {
            foreach (var g in Gamepad.all) if (g.startButton.wasPressedThisFrame) return true;
            var k = Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }

        public static bool DevHudPressed()
        {
            var k = Keyboard.current;
            return k != null && k.f3Key.wasPressedThisFrame;
        }
    }

    public sealed class GamepadControls : Controls
    {
        public readonly Gamepad Pad;
        public GamepadControls(Gamepad pad) { Pad = pad; }
        bool On => Pad != null && Pad.added;
        public override string Label => "Gamepad";
        public override bool UsesMouse => false;
        static Vector2 Dead(Vector2 v, float d = 0.2f) => v.magnitude < d ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
        public override Vector2 Move => On ? Dead(Pad.leftStick.ReadValue() + Pad.dpad.ReadValue()) : Vector2.zero;
        public override Vector2 AimStick => On ? Dead(Pad.rightStick.ReadValue(), 0.3f) : Vector2.zero;
        public override bool Fire => On && Pad.rightTrigger.isPressed;
        public override bool AimDownSights => On && Pad.leftTrigger.isPressed;
        public override bool Run => On && (Pad.leftStickButton.isPressed || Pad.leftShoulder.isPressed);
        public override bool RunPressed => On && (Pad.leftStickButton.wasPressedThisFrame || Pad.leftShoulder.wasPressedThisFrame);
        public override bool Crouch => On && Pad.buttonEast.wasPressedThisFrame;
        public override bool Reload => On && Pad.buttonWest.wasPressedThisFrame;
        public override bool Special => On && Pad.buttonNorth.wasPressedThisFrame;
        public override bool Ultimate => On && Pad.rightShoulder.wasPressedThisFrame;
        public override bool Interact => On && Pad.buttonSouth.isPressed;
        public override bool Confirm => On && Pad.buttonSouth.wasPressedThisFrame;
        public override bool AnyJoin => On && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame);
        public override bool CameraToggle => On && Pad.selectButton.wasPressedThisFrame;
    }

    public sealed class KeyboardControls : Controls
    {
        public readonly int Layout; // 0 = WASD + mouse, 1 = arrows (couch P2, aims where it walks)
        public KeyboardControls(int layout) { Layout = layout; }
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        public override string Label => Layout == 0 ? "Keyboard + Mouse" : "Keyboard (arrows)";
        public override bool UsesMouse => Layout == 0 && M != null;
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
        public override Vector2 AimStick => Vector2.zero;
        public override bool Fire => Layout == 0 ? M != null && M.leftButton.isPressed : Held(Key.Comma);
        public override bool AimDownSights => Layout == 0 && M != null && M.rightButton.isPressed;
        public override bool Run => Layout == 0 ? Held(Key.LeftShift) : Held(Key.RightShift);
        public override bool RunPressed => Layout == 0 ? Pressed(Key.LeftShift) : Pressed(Key.RightShift);
        public override bool Crouch => Layout == 0 ? Pressed(Key.LeftCtrl) || Pressed(Key.C) : Pressed(Key.RightCtrl);
        public override bool Reload => Layout == 0 ? Pressed(Key.R) : Pressed(Key.Period);
        public override bool Special => Layout == 0 ? Pressed(Key.E) : Pressed(Key.Slash);
        public override bool Ultimate => Layout == 0 ? Pressed(Key.Q) : Pressed(Key.Quote);
        public override bool Interact => Layout == 0 ? Held(Key.F) : Held(Key.Enter);
        public override bool Confirm => Layout == 0 ? Pressed(Key.Enter) || Pressed(Key.Space) || Pressed(Key.NumpadEnter) : Pressed(Key.Enter);
        public override bool AnyJoin => Layout == 0
            ? Pressed(Key.Enter) || Pressed(Key.Space) || (M != null && M.leftButton.wasPressedThisFrame)
            : Pressed(Key.Comma) || Pressed(Key.Slash);
        public override bool CameraToggle => Layout == 0 && Pressed(Key.Tab);
    }
}
