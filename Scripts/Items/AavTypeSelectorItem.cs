#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEditor;
using UnityEngine;

using AnimatorAsCode.V0;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    public class AavTypeSelectorItem : AavMenuItem
    {
        public override string GUIName => "Type Select";

        private List<(Type, string)> AvailableTypes;

        public Action<AavTypeSelectorItem, Type> TypeSelected;

        public override void GenerateAnimator(AacFlBase aac, AnimatorAsVisual aav, List<string> usedAv3Parameters)
        {
            Debug.LogWarning("You still have an Entry in your menu without a type selected! You should probably change that...");
        }

        void OnEnable()
        {
            AvailableTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t != typeof(AavMenuItem) &&
                    t != typeof(AavTypeSelectorItem) && // important to avoid recursion
                    typeof(AavMenuItem).IsAssignableFrom(t))
                .Select(t => {
                    var tempGo = new GameObject("Temp");
                    var instance = (AavMenuItem)tempGo.AddComponent(t);
                    var name = instance.GUIName;
                    var order = instance.GUISortOrder;
                    DestroyImmediate(tempGo);
                    return ((t, name), order);
                })
                .OrderBy(x => x.order)
                .Select(x => x.Item1)
                .ToList();
        }

        public override bool DrawEditor()
        {
            var headerStyle = AavHelpers.HeaderStyle;
            GUILayout.Label("Select Type", headerStyle);

            foreach (var (type, name) in AvailableTypes)
            {
                if (GUILayout.Button(name, AavHelpers.BigButtonStyle))
                {
                    TypeSelected?.Invoke(this, type);
                    return true;
                }
            }

            return false;
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            return null;
        }
    }
}
#endif