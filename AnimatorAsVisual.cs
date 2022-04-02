#if UNITY_EDITOR

using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace pi.AnimatorAsVisual
{
    /*
        Main serialized class
    */
    public class AnimatorAsVisual : MonoBehaviour
    {
        public VRCAvatarDescriptor Avatar;

        public AavSubmenuItem Root;

        [Tooltip("VRChat recommends 'Write Defaults' set to off, but it may be useful to enable in some scenarios. Leave untouched/off if you're unsure.")]
        public bool WriteDefaults;

        // for editor convenience *only*
        [HideInInspector] public bool Dirty = false;
        [HideInInspector] public int CurrentlySelected = -1;
        [HideInInspector, SerializeReference] public AavSubmenuItem CurrentMenu;
    }
}

#endif