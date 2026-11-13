using UnityEngine;
using UnityEngine.InputSystem;

namespace Lanternfall
{
    /// <summary>
    /// One player's input. Gamepads first (couch co-op); two keyboard layouts so you can test without pads.
    ///   Gamepad:  stick/d-pad move · X/□ attack · Y/△ special · A/✕ lantern + revive · Start begin
    ///   Keys P1:  WASD · J attack · K special · L lantern · Enter begin
    ///   Keys P2:  arrows · , attack · . special · / lantern
    /// </summary>
    public abstract class Controls
    {
        public abstract string Label { get; }
        public abstract bool Connected { get; }
        public abstract Vector2 Move { get; }
        public abstract bool Attack { get; }      // held = auto-fire
        public abstract bool Special { get; }     // pressed this frame
        public abstract bool Interact { get; }    // pressed this frame
        public abstract bool InteractHeld { get; }
        public abstract bool AnyJoin { get; }

        public static bool StartPressed()
        {
            foreach (var g in Gamepad.all) if (g.startButton.wasPressedThisFrame) return true;
            var k = Keyboard.current;
            return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
        }
    }

    public sealed class GamepadControls : Controls
    {
        public readonly Gamepad Pad;
        public GamepadControls(Gamepad pad) { Pad = pad; }
        public override string Label => "Pad";
        public override bool Connected => Pad != null && Pad.added;
        public override Vector2 Move
        {
            get
            {
                if (!Connected) return Vector2.zero;
                var v = Pad.leftStick.ReadValue() + Pad.dpad.ReadValue();
                return v.sqrMagnitude < 0.04f ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
            }
        }
        public override bool Attack => Connected && (Pad.buttonWest.isPressed || Pad.rightTrigger.isPressed);
        public override bool Special => Connected && (Pad.buttonNorth.wasPressedThisFrame || Pad.rightShoulder.wasPressedThisFrame);
        public override bool Interact => Connected && Pad.buttonSouth.wasPressedThisFrame;
        public override bool InteractHeld => Connected && Pad.buttonSouth.isPressed;
        public override bool AnyJoin => Connected && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonWest.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame);
    }

    public sealed class KeyboardControls : Controls
    {
        readonly int layout; // 0 = WASD, 1 = arrows
        public readonly int Layout;
        public KeyboardControls(int layout) { this.layout = layout; Layout = layout; }
        static Keyboard K => Keyboard.current;
        public override string Label => layout == 0 ? "Keys WASD" : "Keys Arrows";
        public override bool Connected => K != null;
        Key Up => layout == 0 ? Key.W : Key.UpArrow;
        Key Down => layout == 0 ? Key.S : Key.DownArrow;
        Key Left => layout == 0 ? Key.A : Key.LeftArrow;
        Key Right => layout == 0 ? Key.D : Key.RightArrow;
        Key AttackKey => layout == 0 ? Key.J : Key.Comma;
        Key SpecialKey => layout == 0 ? Key.K : Key.Period;
        Key InteractKey => layout == 0 ? Key.L : Key.Slash;

        public override Vector2 Move
        {
            get
            {
                if (K == null) return Vector2.zero;
                var v = new Vector2((K[Right].isPressed ? 1 : 0) - (K[Left].isPressed ? 1 : 0), (K[Up].isPressed ? 1 : 0) - (K[Down].isPressed ? 1 : 0));
                return v.sqrMagnitude > 1 ? v.normalized : v;
            }
        }
        public override bool Attack => K != null && K[AttackKey].isPressed;
        public override bool Special => K != null && K[SpecialKey].wasPressedThisFrame;
        public override bool Interact => K != null && K[InteractKey].wasPressedThisFrame;
        public override bool InteractHeld => K != null && K[InteractKey].isPressed;
        public override bool AnyJoin => K != null && (K[AttackKey].wasPressedThisFrame || K[InteractKey].wasPressedThisFrame);
    }
}
