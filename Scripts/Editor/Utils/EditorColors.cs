using UnityEditor;
using UnityEngine;

namespace UnityEventTracker.Utils
{
    public static class EditorColors
    {
        private static readonly Color32 DarkBackground  = new Color32(56, 56, 56, 255);
        private static readonly Color32 LightBackground = new Color32(200, 200, 200, 255);

        /// <summary>
        /// Returns the background color for a hierarchy object that is not selected.
        /// </summary>
        public static Color32 Background => EditorGUIUtility.isProSkin ? DarkBackground : LightBackground;
    }
}