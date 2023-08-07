#if UNITY_EDITOR

using System;
using System.Collections.Generic;
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

                AacFlState shown, hidden;
                if (Default)
                {
                    shown = GenerateSimpleState(aac, fx, true);
                    hidden = GenerateSimpleState(aac, fx, false);
                }
                else
                {
                    hidden = GenerateSimpleState(aac, fx, false);
                    shown = GenerateSimpleState(aac, fx, true);
                }

                var param = gen.MakeAv3Parameter(fx, this.ParameterName, this.Saved, this.Default);

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
                gen.RegisterBlendTreeMotion(GenerateSimpleClip(aac, true).Clip, GenerateSimpleClip(aac, false).Clip, param);
            }
        }

        private AacFlClip GenerateSimpleClip(AacFlBase aac, bool enabled)
        {
            var clip = aac.NewClip();
            foreach (var toggle in this.Toggles)
            {
                clip = clip.Toggling(toggle.Object, enabled ^ toggle.Invert);
            }
            foreach (var blend in this.BlendShapes)
            {
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

        private AacFlState GenerateSimpleState(AacFlBase aac, AacFlLayer fx, bool enabled)
        {
            var clip = GenerateSimpleClip(aac, enabled);
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