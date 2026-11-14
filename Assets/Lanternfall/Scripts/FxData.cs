// Generated from the pixelfx sprite-sheet JSON in Tools/fx-meta (meta.pixelfx).
// Regenerate with Tools/render-fx.sh after re-rendering the sheets.
using System.Collections.Generic;

namespace Lanternfall
{
    public readonly struct FxInfo
    {
        public readonly int Fps, Frames, Columns, W, H, PivotX, PivotY;
        public readonly bool Loop;
        /// <summary>Seconds from start to the first contact hit (pixelfx act), 0 if none.</summary>
        public readonly float Contact;
        public FxInfo(int fps, bool loop, int frames, int columns, int w, int h, int px, int py, float contact)
        { Fps = fps; Loop = loop; Frames = frames; Columns = columns; W = w; H = h; PivotX = px; PivotY = py; Contact = contact; }
    }

    public static class FxData
    {
        public static readonly Dictionary<string, FxInfo> All = new Dictionary<string, FxInfo>
        {
            { "blink-out", new FxInfo(30, false, 49, 6, 136, 96, 68, 80, 0f) },
            { "blizzard-vortex", new FxInfo(30, true, 60, 8, 128, 116, 64, 102, 0f) },
            { "bullet-impact", new FxInfo(30, false, 43, 6, 152, 100, 116, 84, 0f) },
            { "burning", new FxInfo(30, true, 36, 7, 72, 80, 36, 70, 0f) },
            { "chain-lightning", new FxInfo(30, false, 55, 5, 248, 104, 40, 82, 0.05f) },
            { "crit-hit", new FxInfo(30, false, 28, 5, 216, 136, 84, 106, 0f) },
            { "dark-explosion", new FxInfo(30, false, 115, 11, 280, 248, 140, 206, 0f) },
            { "eclipse", new FxInfo(30, true, 120, 11, 160, 144, 80, 72, 0f) },
            { "fire-ring", new FxInfo(30, false, 118, 9, 224, 128, 112, 92, 0.32f) },
            { "fireball", new FxInfo(30, true, 36, 4, 136, 56, 112, 28, 0f) },
            { "frost-nova", new FxInfo(30, false, 103, 9, 288, 192, 144, 150, 0.42f) },
            { "frozen", new FxInfo(30, true, 60, 8, 72, 64, 36, 54, 0f) },
            { "ground-slam", new FxInfo(30, false, 97, 9, 288, 208, 144, 164, 0f) },
            { "heal-aoe", new FxInfo(30, false, 88, 7, 256, 136, 128, 104, 0f) },
            { "heal-rose", new FxInfo(30, false, 88, 7, 256, 136, 128, 104, 0f) },
            { "heart-pickup", new FxInfo(30, true, 36, 7, 56, 60, 28, 52, 0f) },
            { "hit-flash", new FxInfo(30, false, 16, 4, 80, 64, 40, 54, 0f) },
            { "hit-spark", new FxInfo(30, false, 16, 4, 136, 80, 44, 64, 0f) },
            { "ice-lance", new FxInfo(30, true, 24, 3, 176, 64, 146, 32, 0f) },
            { "ice-shatter", new FxInfo(30, false, 85, 7, 224, 112, 68, 90, 0f) },
            { "level-up", new FxInfo(30, false, 97, 9, 176, 138, 88, 114, 0.28f) },
            { "lightning-strike", new FxInfo(30, false, 91, 11, 240, 264, 120, 228, 0f) },
            { "magic-circle-cast", new FxInfo(30, false, 118, 12, 200, 220, 100, 192, 0f) },
            { "meteor", new FxInfo(30, false, 151, 12, 272, 232, 150, 208, 0f) },
            { "muzzle-smg", new FxInfo(30, true, 30, 5, 112, 72, 56, 66, 0f) },
            { "plasma-explosion", new FxInfo(30, false, 52, 7, 224, 200, 112, 100, 0f) },
            { "poof", new FxInfo(30, false, 49, 7, 112, 92, 56, 80, 0f) },
            { "shadow-bolt", new FxInfo(30, true, 36, 4, 136, 56, 112, 28, 0f) },
            { "shadow-flames", new FxInfo(30, true, 72, 9, 104, 112, 52, 102, 0f) },
            { "shatter-death", new FxInfo(30, false, 100, 8, 172, 96, 84, 84, 0.5f) },
            { "smite", new FxInfo(30, false, 85, 9, 224, 176, 112, 148, 0f) },
            { "spawn-portal", new FxInfo(30, false, 115, 10, 208, 150, 104, 122, 0f) },
            { "spirit-flame", new FxInfo(30, true, 90, 12, 80, 124, 40, 94, 0f) },
            { "stunned", new FxInfo(30, true, 60, 8, 60, 64, 30, 56, 0f) },
            { "summon-rise", new FxInfo(30, false, 139, 10, 176, 116, 88, 94, 0f) },
            { "sunburst", new FxInfo(30, true, 120, 11, 128, 128, 64, 64, 0f) },
            { "void-orb", new FxInfo(30, true, 96, 10, 168, 160, 84, 150, 0f) },
            { "xp-orb", new FxInfo(30, true, 54, 7, 72, 56, 36, 48, 0f) },
            { "zap-hit", new FxInfo(30, false, 28, 5, 144, 112, 76, 56, 0f) },
        };
    }
}
