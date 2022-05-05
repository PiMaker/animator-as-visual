#if UNITY_EDITOR

using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    /*
        Main serialized class
    */
    public class AnimatorAsVisual : MonoBehaviour
    {
        public VRCAvatarDescriptor Avatar;

        [Tooltip("This will default to the menu on your Avatar if left blank, but you can also specify a specific menu here to not overwrite existing entries. You can then use that menu as a 'sub menu' in a manually created expressions menu.")]
        public VRCExpressionsMenu Menu;

        [SerializeReference] public AavSubmenuItem Root;

        [Tooltip("VRChat recommends 'Write Defaults' set to off, but it may be useful to enable in some scenarios. Leave untouched/off if you're unsure.")]
        public bool WriteDefaults;

        // for editor convenience *only*
        [HideInInspector] public bool Dirty = false;
        [HideInInspector] public int CurrentlySelected = -1;
        [HideInInspector, SerializeReference] public AavSubmenuItem CurrentMenu;
    }
}

#endif