#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

namespace pi.AnimatorAsVisual
{
    public static class AavHelpers
    {
        public static T UpdateWith<T>(this T cur, Func<T> ui, ref bool changed)
        {
            T n = ui();
            if (n == null && cur == null) return n;
            if ((cur == null && n != null) || (cur != null && n == null) || !cur.Equals(n)) changed = true;
            return n;
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                var headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize += 2;
                headerStyle.fixedHeight = 30;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                return headerStyle;
            }
        }

        public static GUIStyle BigButtonStyle
        {
            get
            {
                var bigButton = new GUIStyle(GUI.skin.button);
                bigButton.fixedHeight = 32.0f;
                bigButton.fontStyle = FontStyle.Bold;
                bigButton.fontSize = 14;
                return bigButton;
            }
        }

        public static readonly GUIContent LblOn = new GUIContent("On", "The value to use when this item is enabled.");
        public static readonly GUIContent LblOff = new GUIContent("Off", "The value to use when this item is disabled.");
    }
}
#endif