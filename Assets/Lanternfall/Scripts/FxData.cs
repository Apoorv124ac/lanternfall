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
            { "burning", new FxInfo(30, true, 36, 7, 72, 80, 36, 70, 0f) },
            { "chain-lightning", new FxInfo(30, false, 55, 5, 248, 104, 40, 82, 0.05f) },
            { "dark-explosion", new FxInfo(30, false, 115, 11, 280, 248, 140, 206, 0f) },
            { "fire-ring", new FxInfo(30, false, 118, 9, 224, 128, 112, 92, 0.32f) },
            { "fireball", new FxInfo(30, true, 36, 4, 136, 56, 112, 28, 0f) },
            { "frost-nova", new FxInfo(30, false, 103, 9, 288, 192, 144, 150, 0.42f) },
            { "frozen", new FxInfo(30, true, 60, 8, 72, 64, 36, 54, 0f) },
            { "heal-aoe", new FxInfo(30, false, 88, 7, 256, 136, 128, 104, 0f) },
            { "heal-rose", new FxInfo(30, false, 88, 7, 256, 136, 128, 104, 0f) },
            { "hit-spark", new FxInfo(30, false, 16, 4, 136, 80, 44, 64, 0f) },
            { "ice-lance", new FxInfo(30, true, 24, 3, 176, 64, 146, 32, 0f) },
            { "ice-shatter", new FxInfo(30, false, 85, 7, 224, 112, 68, 90, 0f) },
            { "plasma-explosion", new FxInfo(30, false, 52, 7, 224, 200, 112, 100, 0f) },
            { "poof", new FxInfo(30, false, 49, 7, 112, 92, 56, 80, 0f) },
            { "smite", new FxInfo(30, false, 85, 9, 224, 176, 112, 148, 0f) },
            { "spawn-portal", new FxInfo(30, false, 115, 10, 208, 150, 104, 122, 0f) },
            { "spirit-flame", new FxInfo(30, true, 90, 12, 80, 124, 40, 94, 0f) },
            { "stunned", new FxInfo(30, true, 60, 8, 60, 64, 30, 56, 0f) },
            { "zap-hit", new FxInfo(30, false, 28, 5, 144, 112, 76, 56, 0f) },
        };
    }
}
