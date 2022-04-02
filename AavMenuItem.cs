#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V0;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [ExecuteInEditMode]
    public abstract class AavMenuItem : ScriptableObject
    {
        /*
            Basic data for one entry
        */

        public string AavName;
        public Texture2D Icon;

        // note: substringing a Guid is normally a very bad idea, but here it is only
        // done as a "last defense" anyway, so it'll be 大丈夫
        [SerializeField] private string UUID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);

        public string ParameterName => AavName + "-" + UUID;

        public abstract string GUIName { get; }
        public virtual int GUISortOrder => 0;

        public abstract void GenerateAnimator(AacFlBase aac, AnimatorAsVisual aav, List<string> usedAv3Parameters);
        public abstract VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav);

        // returns true if any element was modified
        public abstract bool DrawEditor();
    }
}

#endif