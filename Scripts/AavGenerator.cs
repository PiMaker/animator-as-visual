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
    public class AavGenerator
    {
        public static AavGenerator Instance { get; private set; }

        public AacFlBase AAC { get; private set; }
        public AnimatorAsVisual AAV { get; private set; }
        public AacFlLayer MainFX { get; private set; }
        
        private readonly List<string> usedParams = new List<string>();
        private readonly List<(Motion on, Motion off, AacFlFloatParameter param)> blendTreeMotions = new List<(Motion on, Motion off, AacFlFloatParameter param)>();

        public int StatsBlendTreeMotions => blendTreeMotions.Count;
        public int StatsUsedParameters => usedParams.Count;
        public int StatsUpdatedUsedParameters { get; private set; }
        public int StatsLayers { get; private set; }

        public AavGenerator(AnimatorAsVisual aav)
        {
            this.AAV = aav;
            if (Instance != null)
                Debug.LogError("AavGenerator already exists!");
            Instance = this;
        }

        public void Generate()
        {
            var avatar = AAV.Avatar;
            usedParams.Clear();
            blendTreeMotions.Clear();
            StatsUpdatedUsedParameters = 0;
            StatsLayers = 0;

            // TODO: Move somewhere more generic
            var remotingRoot = AAV.Avatar.transform.Find("AAV-Remoting-Root")?.gameObject;
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
            AAC = AacV0.Create(new AacConfiguration
            {
                SystemName = systemName,
                AvatarDescriptor = avatar,
                AnimatorRoot = avatar.transform,
                DefaultValueRoot = avatar.transform,
                AssetContainer = fx,
                AssetKey = "AnimatorAsVisual",
                DefaultsProvider = new AacDefaultsProvider(writeDefaults: AAV.WriteDefaults),
            });
            AAC.ClearPreviousAssets();

            // clean previous data
            fx.layers = fx.layers.Where(l => !l.name.StartsWith("AAV-")).ToArray();
            fx.parameters = fx.parameters.Where(p => !p.name.StartsWith("AAV") && !p.name.StartsWith("RemoteAAV")).ToArray();

            AAC.RemoveAllMainLayers();
            MainFX = AAC.CreateMainFxLayer();
            MainFX.WithAvatarMaskNoTransforms(); // FIXME? make masks configurable?

            // generate a layer for every entry
            foreach (var item in AAV.Root.EnumerateRecursive())
            {
                item.GenerateAnimator(this);
            }

            // clean up Av3 parameters
            var ptmp = new List<VRCExpressionParameters.Parameter>(avatar.expressionParameters.parameters ?? new VRCExpressionParameters.Parameter[0]);
            avatar.expressionParameters.parameters = ptmp.Where(p => !p.name.StartsWith("AAV") || usedParams.Contains(p.name)).ToArray();

            if (blendTreeMotions.Count == 0)
            {
                // no need to keep main layer
                fx.layers = fx.layers.Where(l => l.name != systemName).ToArray();
            }
            else
            {
                // use main layer for combined direct blend tree motions
                var tree = AAC.NewBlendTreeAsRaw();
                tree.name = "AAVInternal-BlendTree (WD On)";
                tree.blendType = BlendTreeType.Direct;
                tree.useAutomaticThresholds = false;

                var weight = MainFX.FloatParameter("AAVInternal-BlendTree-Weight");
                MainFX.OverrideValue(weight, 1.0f);

                var childMotions = new List<ChildMotion>();

                foreach (var motion in blendTreeMotions)
                {
                    var childTree = AAC.NewBlendTreeAsRaw();
                    childTree.name = motion.param.Name;
                    childTree.blendType = BlendTreeType.Simple1D;
                    childTree.AddChild(motion.off, 0.0f);
                    childTree.AddChild(motion.on, 1.0f);
                    childTree.blendParameter = motion.param.Name;
                    childMotions.Add(new ChildMotion { motion = childTree, directBlendParameter = weight.Name, threshold = 0.0f, timeScale = 1.0f });
                }

                tree.children = childMotions.ToArray();
                tree.blendParameter = "AAVInternal-BlendTree-Weight";

                MainFX.NewState("AAVInternal-BlendTree State (WD On)").WithAnimation(tree).WithWriteDefaultsSetTo(true);
            }

            StatsLayers = fx.layers.Count(l => l.name.StartsWith("AAV") || l.name.StartsWith("RemoteAAV"));

            // generate Av3 menu
            var menu = AAV.Menu ?? avatar.expressionsMenu;
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
            foreach (var item in AAV.Root.Items)
            {
                var ctrl = item.GenerateAv3MenuEntry(AAV);
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
        public AacFlBoolParameter MakeAv3Parameter(AacFlLayer fx, string name, bool saved, bool @default)
        {
            var param = (AacFlBoolParameter)MakeAv3ParameterInternal(fx, name, saved, VRCExpressionParameters.ValueType.Bool, @default ? 1.0f : 0.0f);
            if (param != null)
                fx.OverrideValue(param, @default);
            return param;
        }
        public AacFlIntParameter MakeAv3Parameter(AacFlLayer fx, string name, bool saved, int @default)
        {
            var param = (AacFlIntParameter)MakeAv3ParameterInternal(fx, name, saved, VRCExpressionParameters.ValueType.Int, (float)@default);
            if (param != null)
                fx.OverrideValue(param, @default);
            return param;
        }
        public AacFlFloatParameter MakeAv3Parameter(AacFlLayer fx, string name, bool saved, float @default)
        {
            var param = (AacFlFloatParameter)MakeAv3ParameterInternal(fx, name, saved, VRCExpressionParameters.ValueType.Float, @default);
            if (param != null)
                fx.OverrideValue(param, @default);
            return param;
        }

        public AacFlFloatParameter MakeAv3ParameterBoolFloat(AacFlLayer fx, string name, bool saved, bool @default)
        {
            var param = (AacFlFloatParameter)MakeAv3ParameterInternal(fx, name, saved, VRCExpressionParameters.ValueType.Float, @default ? 1.0f : 0.0f, true);
            if (param != null)
                fx.OverrideValue(param, @default ? 1.0f : 0.0f);
            return param;
        }

        private AacFlParameter MakeAv3ParameterInternal(AacFlLayer fx, string name, bool saved, VRCExpressionParameters.ValueType type, float @default, bool forceFloatFx = false)
        {
            name = "AAV" + name;

            var Parameters = AAV.Avatar.expressionParameters;
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
                StatsUpdatedUsedParameters++;
            }

            usedParams.Add(name);

            if (forceFloatFx)
                return fx.FloatParameter(name);

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

        public void RegisterBlendTreeMotion(Motion on, Motion off, AacFlFloatParameter param)
        {
            blendTreeMotions.Add((on, off, param));
        }
    }
}

#endif