﻿#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [ExecuteInEditMode]
    public abstract class AavMenuItem : MonoBehaviour
    {
        public string GUIName => GetType().GetCustomAttribute<AavMenuAttribute>()?.GUIName ?? GetType().Name;

        /*
            Basic data for one entry
        */
        public string AavName
        {
            get => this.gameObject?.name;
            set {
                if (this.gameObject.name != value)
                {
                    this.gameObject.name = value;
                }
            }
        }
        public Texture2D Icon;

        // note: substringing a Guid is normally a very bad idea, but here it is only
        // done as a "last defense" anyway, so it'll be 大丈夫
        [SerializeField] private string UUID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);

        public string ParameterName => AavName + "-" + UUID;

        public abstract void GenerateAnimator(AavGenerator gen);
        public abstract VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav);

        // returns true if any element was modified
        public abstract bool DrawEditor(AnimatorAsVisual aav);

        public static void CopyBasicData(AavMenuItem source, AavMenuItem target)
        {
            target.AavName = source.AavName;
            target.Icon = source.Icon;
        }

        public virtual void PreGenerateAnimator1(AavGenerator gen) {}
        public virtual void PreGenerateAnimator2(AavGenerator gen) {}
        public virtual void PostGenerateAnimator1(AavGenerator gen) {}
        public virtual void PostGenerateAnimator2(AavGenerator gen) {}
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AavMenuAttribute : Attribute
    {
        public readonly string GUIName;
        public readonly int GUISortOrder;

        public AavMenuAttribute(string guiName, int guiSortOrder = 0)
        {
            this.GUIName = guiName;
            this.GUISortOrder = guiSortOrder;
        }
    }
}

#endif