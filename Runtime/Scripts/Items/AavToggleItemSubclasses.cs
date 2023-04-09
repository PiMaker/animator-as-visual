#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace pi.AnimatorAsVisual
{
    public partial class AavToggleItem : AavMenuItem
    {
        [Serializable]
        public class AavGameObjectToggle
        {
            public GameObject Object;
            public bool Invert;
        }

        [Serializable]
        public class AavBlendShapeToggle
        {
            public SkinnedMeshRenderer Renderer;
            public string BlendShape;
            public float StateOn = 100.0f;
            public float StateOff = 0.0f;

            [NonSerialized] public int CurBlendIndex;
            [NonSerialized] public string[] CurBlendList;
        }

        [Serializable]
        public class AavMaterialSwapToggle
        {
            public MeshRenderer Renderer;
            public int Slot;
            public Material MaterialOn, MaterialOff;
        }

        [Serializable]
        public enum AavMaterialParamType
        {
            Float,
            Color,
        }

        [Serializable]
        public class AavMaterialParamToggle
        {
            public Renderer Renderer;
            public string Property;

            public AavMaterialParamType Type;

            public float FloatValueOn = 1.0f;
            public float FloatValueOff = 0.0f;
            public Color ColorValueOn = Color.white;
            public Color ColorValueOff = Color.black;
            public bool ColorIsHDR;

            [NonSerialized] internal Material[] CachedSharedMaterials;
            [NonSerialized] internal Dictionary<string, (Material, int)> PropertyCache;
            [NonSerialized] internal int CurPropertyIndex;
            [NonSerialized] internal string[] CurPropertyList;

            internal void UpdateMaterialPropertyCache(Renderer r)
            {
                this.CachedSharedMaterials = r.sharedMaterials.ToArray(); // Clone

                this.PropertyCache = new Dictionary<string, (Material, int)>();
                foreach (var mat in r.sharedMaterials)
                {
                    var s = mat.shader;
                    var propCount = s.GetPropertyCount();
                    for (int j = 0; j < propCount; j++)
                    {
                        var name = s.GetPropertyName(j);
                        if (!this.PropertyCache.ContainsKey(name))
                        {
                            this.PropertyCache.Add(name, (mat, j));
                        }
                    }
                }

                var parameterNames = new string[this.PropertyCache.Count + 1];
                parameterNames[0] = "< Select Parameter >";
                var i = 0;
                var curIndex = -1;
                foreach (var p in this.PropertyCache.Keys.OrderBy(x => x))
                {
                    var data = this.PropertyCache[p];
                    parameterNames[++i] = p + " (" + data.Item1.shader.GetPropertyDescription(data.Item2) + ")";
                    if (p == this.Property)
                    {
                        curIndex = i;
                    }
                }

                this.CurPropertyIndex = curIndex;
                this.CurPropertyList = parameterNames;
            }
        }
    }
}
#endif