#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using AnimatorAsCode.V0;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    public static class AavGenerator
    {
        public static void Generate(AnimatorAsVisual aav)
        {
            var avatar = aav.Avatar;
            var usedParams = new List<string>();

            // TODO: Move somewhere more generic
            var remotingRoot = aav.Avatar.transform.Find("AAV-Remoting-Root")?.gameObject;
            if (remotingRoot != null)
            {
                var removeThese = new List<GameObject>();
                for (int i = 0; i < remotingRoot.transform.childCount; i++)
                    removeThese.Add(remotingRoot.transform.GetChild(i).gameObject);
                removeThese.ForEach(go => GameObject.DestroyImmediate(go));
            }

            // Generate AAC instance
            var fx = (AnimatorController)avatar.baseAnimationLayers[4].animatorController;
            var systemName = "AAV-" + avatar.gameObject.name;
            var aac = AacV0.Create(new AacConfiguration
            {
                SystemName = systemName,
                AvatarDescriptor = avatar,
                AnimatorRoot = avatar.transform,
                DefaultValueRoot = avatar.transform,
                AssetContainer = fx,
                AssetKey = "AnimatorAsVisual",
                DefaultsProvider = new AacDefaultsProvider(writeDefaults: aav.WriteDefaults),
            });
            aac.ClearPreviousAssets();

            // clean previous data
            fx.layers = fx.layers.Where(l => !l.name.StartsWith("AAV-")).ToArray();
            fx.parameters = fx.parameters.Where(p => !p.name.StartsWith("AAV")).ToArray();

            aac.RemoveAllMainLayers();
            var mainFx = aac.CreateMainFxLayer();
            mainFx.WithAvatarMaskNoTransforms(); // FIXME? make masks configurable?

            // generate a layer for every entry
            foreach (var item in aav.Root.EnumerateRecursive())
            {
                item.GenerateAnimator(aac, aav, usedParams);
            }

            // clean up Av3 parameters
            var ptmp = new List<VRCExpressionParameters.Parameter>(avatar.expressionParameters.parameters ?? new VRCExpressionParameters.Parameter[0]);
            avatar.expressionParameters.parameters = ptmp.Where(p => !p.name.StartsWith("AAV") || usedParams.Contains(p.name)).ToArray();

            // we don't actually want the mainFx layer, it is empty anyway
            fx.layers = fx.layers.Where(l => l.name != systemName).ToArray();

            // generate Av3 menu
            var menu = aav.Menu ?? avatar.expressionsMenu;
            menu.controls.Clear();
            var allMenus = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(menu));
            foreach (var oldMenu in allMenus)
            {
                if (AssetDatabase.IsSubAsset(oldMenu))
                {
                    // destroy old sub menu data
                    ScriptableObject.DestroyImmediate(oldMenu, true);
                }
            }
            foreach (var item in aav.Root.Items)
            {
                var ctrl = item.GenerateAv3MenuEntry(aav);
                if (ctrl != null)
                    menu.controls.Add(ctrl);
            }

            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(avatar.expressionsMenu);
            EditorUtility.SetDirty(avatar.expressionParameters);

            Debug.Log("AAV: Synchronized successfully!");
        }

        /*
            Helper Functions
        */
        public static AacFlBoolParameter MakeAv3Parameter(AnimatorAsVisual aav, List<string> usedParams, AacFlLayer fx, string name, bool saved, bool @default)
        {
            return (AacFlBoolParameter)MakeAv3Parameter(aav, usedParams, fx, name, saved, VRCExpressionParameters.ValueType.Bool, @default ? 1.0f : 0.0f);
        }
        public static AacFlIntParameter MakeAv3Parameter(AnimatorAsVisual aav, List<string> usedParams, AacFlLayer fx, string name, bool saved, int @default)
        {
            return (AacFlIntParameter)MakeAv3Parameter(aav, usedParams, fx, name, saved, VRCExpressionParameters.ValueType.Int, (float)@default);
        }
        public static AacFlFloatParameter MakeAv3Parameter(AnimatorAsVisual aav, List<string> usedParams, AacFlLayer fx, string name, bool saved, float @default)
        {
            return (AacFlFloatParameter)MakeAv3Parameter(aav, usedParams, fx, name, saved, VRCExpressionParameters.ValueType.Float, @default);
        }
        public static AacFlParameter MakeAv3Parameter(AnimatorAsVisual aav, List<string> usedParams, AacFlLayer fx, string name, bool saved, VRCExpressionParameters.ValueType type, float @default)
        {
            name = "AAV" + name;

            var Parameters = aav.Avatar.expressionParameters;
            var update = false;
            var parm = Parameters.FindParameter(name);
            if (parm == null) update = true;
            else
            {
                if (parm.valueType != type || parm.defaultValue != @default || parm.saved != saved)
                {
                    var ptmp = new List<VRCExpressionParameters.Parameter>(Parameters.parameters ?? new VRCExpressionParameters.Parameter[0]);
                    ptmp.Remove(parm);
                    Parameters.parameters = ptmp.ToArray();
                    parm = null;
                    update = true;
                }
            }

            if (update)
            {
                var ptmp = new List<VRCExpressionParameters.Parameter>(Parameters.parameters ?? new VRCExpressionParameters.Parameter[0]);
                ptmp.Add(parm = new VRCExpressionParameters.Parameter()
                {
                    name = name,
                    valueType = type,
                    saved = saved,
                    defaultValue = @default,
                });
                Parameters.parameters = ptmp.ToArray();
                Debug.Log("AAV: Added or updated Avatar Parameter: " + name);
            }

            usedParams.Add(name);

            switch (type)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return fx.BoolParameter(name);
                case VRCExpressionParameters.ValueType.Int:
                    return fx.IntParameter(name);
                case VRCExpressionParameters.ValueType.Float:
                    return fx.FloatParameter(name);
                default:
                    return null;
            }
        }
    }
}

#endif