#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEngine;

using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Submenu", -100)]
    public class AavSubmenuItem : AavMenuItem
    {
        public AavSubmenuItem Parent => this.transform.parent?.GetComponent<AavSubmenuItem>();
        public IEnumerable<AavMenuItem> Items => Enumerable.Range(0, this.transform.childCount).Select(i =>
            {
                AavMenuItem item = null;
                var exists = this.transform.GetChild(i)?.TryGetComponent<AavMenuItem>(out item);
                return exists.GetValueOrDefault(false) ? item : null;
            })
            .Where(x => x != null);

        public IEnumerable<AavMenuItem> EnumerateRecursive()
        {
            foreach (var item in this.Items)
            {
                yield return item;

                AavSubmenuItem sub;
                if ((sub = item as AavSubmenuItem) != null)
                {
                    foreach (var subItem in sub.EnumerateRecursive())
                    {
                        yield return subItem;
                    }
                }
            }
        }

        public override void GenerateAnimator(AavGenerator gen)
        {
            // do nothing
        }

        // Note: This draws the editor for the submenu item *itself*,
        // entering a submenu has special handling code in AavEditor.
        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            return false;
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            subMenu.name = this.AavName;
            AssetDatabase.AddObjectToAsset(subMenu, AssetDatabase.GetAssetPath(aav.Avatar.expressionsMenu));
            foreach (var item in this.Items)
            {
                if (item.isActiveAndEnabled && item.gameObject.activeInHierarchy)
                    subMenu.controls.Add(item.GenerateAv3MenuEntry(aav));
            }

            return new VRCExpressionsMenu.Control()
            {
                name = this.AavName,
                icon = this.Icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = subMenu,
            };
        }
    }
}
#endif