#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.Pi.V0;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Toggle", -10)]
    public partial class AavToggleItem : AavMenuItem
    {
        public bool Default = false;
        public bool Saved = true;

        public bool DisableMouthMovement = false;
        public float TransitionDuration = 0.0f;
        public bool AllowRemoteToggle = false;

        public List<AavGameObjectToggle> Toggles = new List<AavGameObjectToggle>();
        public List<AavBlendShapeToggle> BlendShapes = new List<AavBlendShapeToggle>();
        //public List<AavMaterialSwapToggle> MaterialSwaps = new List<AavMaterialSwapToggle>();
        public List<AavMaterialParamToggle> MaterialParams = new List<AavMaterialParamToggle>();

        public List<AavToggleDrives> Drives = new List<AavToggleDrives>();

        public bool CanUseBlendTree => !this.DisableMouthMovement && Mathf.Approximately(TransitionDuration, 0.0f) && (Drives == null || Drives.Count == 0);

        private static readonly HashSet<(SkinnedMeshRenderer, string)> drivenBlendShapes = new HashSet<(SkinnedMeshRenderer, string)>();
        private static readonly Dictionary<(SkinnedMeshRenderer, string), List<AacFlParameter>> doubleDrivenBlendShapes = new Dictionary<(SkinnedMeshRenderer, string), List<AacFlParameter>>();
        private static readonly Dictionary<(SkinnedMeshRenderer, string), (float on, float off)> doubleDrivenBlendShapesValues = new Dictionary<(SkinnedMeshRenderer, string), (float on, float off)>();
        private static bool doubleDrivenBlendShapesUpdated = false;

        public override void PreGenerateAnimator1(AavGenerator gen)
        {
            drivenBlendShapes.Clear();
            doubleDrivenBlendShapes.Clear();
            doubleDrivenBlendShapesValues.Clear();
            doubleDrivenBlendShapesUpdated = false;
        }

        public override void PreGenerateAnimator2(AavGenerator gen)
        {
            foreach (var blend in this.BlendShapes)
            {
                var hash = (blend.Renderer, blend.BlendShape);
                if (drivenBlendShapes.Contains(hash))
                {
                    if (!doubleDrivenBlendShapes.ContainsKey(hash))
                    {
                        doubleDrivenBlendShapes.Add(hash, new List<AacFlParameter>());
                    }
                }
                else
                {
                    drivenBlendShapes.Add(hash);
                }
            }
        }

        public override void PostGenerateAnimator1(AavGenerator gen)
        {
            if (doubleDrivenBlendShapesUpdated)
                return;
            doubleDrivenBlendShapesUpdated = true;

            var aac = gen.AAC;
            foreach (var kvp in doubleDrivenBlendShapes)
            {
                Debug.LogWarning($"Blendshape {kvp.Key.Item2} on {kvp.Key.Item1.name} is driven by multiple AAV items, attempting workaround (experimental).");
                var fx = aac.CreateSupportingFxLayer("AAVDoubleDrivenBlendShape-" + Guid.NewGuid().ToString());
                fx.WithAvatarMaskNoTransforms();

                var stateOff = fx.NewState("Disabled");
                var stateOn = fx.NewState("Enabled");

                var blendStates = doubleDrivenBlendShapesValues[kvp.Key];

                stateOff.WithAnimation(aac.NewClip().BlendShape(kvp.Key.Item1, kvp.Key.Item2, blendStates.off));
                stateOn.WithAnimation(aac.NewClip().BlendShape(kvp.Key.Item1, kvp.Key.Item2, blendStates.on));

                var onTransitions = stateOn.TransitionsTo(stateOff).WhenConditions();
                var offTransitions = stateOff.TransitionsTo(stateOn).WhenConditions();

                var firstOr = true;

                foreach (var param in kvp.Value)
                {
                    if (param is AacFlBoolParameter boolParam)
                    {
                        if (firstOr)
                            onTransitions.And(boolParam.IsTrue());
                        else
                            onTransitions.Or().When(boolParam.IsTrue());
                        offTransitions.And(boolParam.IsFalse());
                    }
                    else if (param is AacFlFloatParameter floatParam)
                    {
                        if (firstOr)
                            onTransitions.And(floatParam.IsGreaterThan(0.5f));
                        else
                            onTransitions.Or().When(floatParam.IsGreaterThan(0.5f));
                        offTransitions.And(floatParam.IsLessThan(0.5f));
                    }
                    else
                    {
                        Debug.LogError($"Blendshape {kvp.Key.Item2} on {kvp.Key.Item1.name} is driven by an unsupported parameter type {param.GetType().Name}. This is an internal error and shouldn't happen, probably?");
                    }

                    firstOr = false;
                }
            }
        }

        /*
            Animator Generation Logic for all kinds of actions (List<T>s above)
        */
        public override void GenerateAnimator(AavGenerator gen)
        {
            var aac = gen.AAC;

            if (!CanUseBlendTree)
            {
                // have to use legacy method with separate layer
                var fx = aac.CreateSupportingFxLayer(this.ParameterName);
                fx.WithAvatarMaskNoTransforms();

                var param = gen.MakeAv3Parameter(fx, this.ParameterName, this.Saved, this.Default);

                AacFlState shown, hidden;
                if (Default)
                {
                    shown = GenerateSimpleState(aac, fx, true, param);
                    hidden = GenerateSimpleState(aac, fx, false, param);
                }
                else
                {
                    hidden = GenerateSimpleState(aac, fx, false, param);
                    shown = GenerateSimpleState(aac, fx, true, param);
                }

                if (Mathf.Approximately(TransitionDuration, 0.0f))
                {
                    shown.TransitionsTo(hidden).When(param.IsFalse());
                    hidden.TransitionsTo(shown).When(param.IsTrue());
                }
                else
                {
                    shown.TransitionsTo(hidden).WithTransitionDurationSeconds(TransitionDuration).When(param.IsFalse());
                    hidden.TransitionsTo(shown).WithTransitionDurationSeconds(TransitionDuration).When(param.IsTrue());
                }

                if (Drives != null && Drives.Count > 0)
                {
                    var activatorStates = new List<AacFlState>();
                    foreach (var drive in Drives)
                    {
                        activatorStates.Clear();
                        switch (drive.When)
                        {
                            case AavToggleDrivesWhen.Always:
                                activatorStates.Add(shown);
                                activatorStates.Add(hidden);
                                break;
                            case AavToggleDrivesWhen.OnActivate:
                                activatorStates.Add(shown);
                                break;
                            case AavToggleDrivesWhen.OnDeactivate:
                                activatorStates.Add(hidden);
                                break;
                        }
                        foreach (var act in activatorStates)
                        {
                            if (drive.Item.CanUseBlendTree)
                                act.Drives(fx.FloatParameter("AAV" + drive.Item.ParameterName), drive.To == AavToggleDrivesTo.TurnOn ? 1.0f : 0.0f);
                            else
                                act.Drives(fx.BoolParameter("AAV" + drive.Item.ParameterName), drive.To == AavToggleDrivesTo.TurnOn);
                        }
                    }
                }
            }
            else
            {
                var param = gen.MakeAv3ParameterBoolFloat(gen.MainFX, this.ParameterName, this.Saved, this.Default);
                gen.RegisterBlendTreeMotion(GenerateSimpleClip(aac, true, param).Clip, GenerateSimpleClip(aac, false, param).Clip, param);
            }
        }

        private AacFlClip GenerateSimpleClip(AacFlBase aac, bool enabled, AacFlParameter param)
        {
            var clip = aac.NewClip();
            foreach (var toggle in this.Toggles)
            {
                clip = clip.Toggling(toggle.Object, enabled ^ toggle.Invert);
            }
            foreach (var blend in this.BlendShapes)
            {
                var hash = (blend.Renderer, blend.BlendShape);
                if (doubleDrivenBlendShapes.TryGetValue(hash, out var paramList))
                {
                    if (doubleDrivenBlendShapesValues.TryGetValue(hash, out var blendStates) && (!Mathf.Approximately(blendStates.on, blend.StateOn) || !Mathf.Approximately(blendStates.off, blend.StateOff)))
                        throw new Exception($"Blendshape {blend.BlendShape} on {blend.Renderer.name} is driven by multiple AAV items with *different on/off values*. This is not supported.");
                    if (!paramList.Any(x => x.Name == param.Name))
                    {
                        paramList.Add(param);
                        doubleDrivenBlendShapesValues[hash] = (on: blend.StateOn, off: blend.StateOff);
                    }
                    continue;
                }
                var blendState = enabled ? blend.StateOn : blend.StateOff;
                clip = clip.BlendShape(blend.Renderer, blend.BlendShape, blendState);
            }
            // foreach (var mswap in this.MaterialSwaps)
            // {
            //     clip = clip.SwappingMaterial(mswap.Renderer, mswap.Slot, enabled ? mswap.MaterialOn : mswap.MaterialOff);
            // }
            clip.Animating(edit =>
            {
                foreach (var mparam in this.MaterialParams)
                {
                    if (mparam.Type == AavMaterialParamType.Float)
                    {
                        edit.Animates(mparam.Renderer, "material." + mparam.Property)
                            .WithOneFrame(enabled ? mparam.FloatValueOn : mparam.FloatValueOff);
                    }
                    else if (mparam.Type == AavMaterialParamType.Color)
                    {
                        edit.AnimatesColor(mparam.Renderer, "material." + mparam.Property)
                            .WithOneFrame(enabled ? mparam.ColorValueOn : mparam.ColorValueOff);
                    }
                }
            });
            return clip;
        }

        private AacFlState GenerateSimpleState(AacFlBase aac, AacFlLayer fx, bool enabled, AacFlParameter param)
        {
            var clip = GenerateSimpleClip(aac, enabled, param);
            var state = fx.NewState(enabled ? "Enabled" : "Disabled").WithAnimation(clip);
            if (this.DisableMouthMovement)
            {
                state.TrackingSets(AacFlState.TrackingElement.Mouth, enabled ? VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation : VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking);
            }
            return state;
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            return new VRCExpressionsMenu.Control()
            {
                name = this.AavName,
                icon = this.Icon,
                // Note: "AAV" gets prefixed by "MakeAv3Parameter", so do it here too
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = "AAV" + this.ParameterName },
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
            };
        }
    }
}
#endif