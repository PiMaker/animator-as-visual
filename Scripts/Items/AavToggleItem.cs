#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AnimatorAsCode.V0;
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

        public List<AavGameObjectToggle> Toggles = new List<AavGameObjectToggle>();
        public List<AavBlendShapeToggle> BlendShapes = new List<AavBlendShapeToggle>();
        public List<AavMaterialSwapToggle> MaterialSwaps = new List<AavMaterialSwapToggle>();
        public List<AavMaterialParamToggle> MaterialParams = new List<AavMaterialParamToggle>();

        /*
            Animator Generation Logic for all kinds of actions (List<T>s above)
        */
        public override void GenerateAnimator(AacFlBase aac, AnimatorAsVisual aav, List<string> usedAv3Parameters)
        {
            aac.RemoveAllSupportingLayers(this.ParameterName);
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

            var param = AavGenerator.MakeAv3Parameter(aav, usedAv3Parameters, fx, this.ParameterName, this.Saved, this.Default);

            shown.TransitionsTo(hidden).When(param.IsFalse());
            hidden.TransitionsTo(shown).When(param.IsTrue());
        }

        private AacFlState GenerateSimpleState(AacFlBase aac, AacFlLayer fx, bool enabled)
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
            foreach (var mswap in this.MaterialSwaps)
            {
                clip = clip.SwappingMaterial(mswap.Renderer, mswap.Slot, enabled ? mswap.MaterialOn : mswap.MaterialOff);
            }
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