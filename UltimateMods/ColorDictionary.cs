using UnityEngine;
using HarmonyLib;

namespace UltimateMods
{
    public static class ColorDictionary
    {
        public static Color ImpostorRed = Palette.ImpostorRed;
        public static Color CrewmateBlue = Palette.CrewmateBlue;
        public static Color ClearWhite = Palette.ClearWhite;
        public static Color EngineerBlue = new Color32(0, 47, 140, byte.MaxValue);
        public static Color SheriffYellow = new Color32(248, 205, 70, byte.MaxValue);
        public static Color JesterPink = new Color32(236, 98, 165, byte.MaxValue);

        public static Color OpportunistGreen = new Color32(0, 255, 0, byte.MaxValue);
    }
}