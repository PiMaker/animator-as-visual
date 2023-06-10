#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Type Select")]
    public class AavTypeSelectorItem : AavMenuItem
    {
        private List<(Type, string)> AvailableTypes;

        public Action<AavTypeSelectorItem, Type> TypeSelected;

        public override void GenerateAnimator(AavGenerator gen)
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
                    var attr = t.GetCustomAttribute<AavMenuAttribute>();
                    var name = attr?.GUIName ?? t.Name;
                    var order = attr?.GUISortOrder ?? 0;
                    return ((t, name), order);
                })
                .OrderBy(x => x.order)
                .Select(x => x.Item1)
                .ToList();
        }

        public override bool DrawEditor(AnimatorAsVisual aav)
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