#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using VRC.SDK3.Avatars.ScriptableObjects;
using static pi.AnimatorAsVisual.AavToggleItem;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Slider")]
    public class AavBlendShapeSlider : AavMenuItem
    {
        [Range(0, 1)]
        public float Default = 0.5f;
        public bool Saved = true;

        // Let's just be lazy and reuse this so we can also reuse the ReorderableList editor
        public List<AavBlendShapeToggle> BlendShapes = new List<AavBlendShapeToggle>();
        public List<AavMaterialParamToggle> MaterialParams = new List<AavMaterialParamToggle>();

        private ReorderableList RLBlendShapes;
        private ReorderableList RLMaterialParams;
        private bool modified = false;

        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            modified = false;

            this.Default = this.Default.UpdateWith(() => EditorGUILayout.Slider(
                new GUIContent("Default State", "The initial value of this slider, when you 'Reset Avatar' in-game or put on the avatar for the first time"),
                    this.Default, 0, 1), ref modified);
            this.Saved = this.Saved.UpdateWith(() => EditorGUILayout.Toggle(
                new GUIContent("Save State", "Should this option be saved, e.g. between worlds or when you restart VRChat."),
                    this.Saved), ref modified);

            if (RLBlendShapes == null)
            {
                RLBlendShapes = new ReorderableList(this.BlendShapes, typeof(AavBlendShapeToggle), true, false, true, true);
                RLBlendShapes.elementHeight *= 2;
                RLBlendShapes.elementHeight += 2;
                RLBlendShapes.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var blend = this.BlendShapes[index];
                    var rendererChanged = false;
                    blend.Renderer = blend.Renderer.UpdateWith(
                        () => (SkinnedMeshRenderer)EditorGUI.ObjectField(
                            new Rect(rect.x, rect.y + 1, rect.width / 2.0f - 5, EditorGUIUtility.singleLineHeight),
                            blend.Renderer, typeof(SkinnedMeshRenderer), true),
                        ref rendererChanged);
                    if (rendererChanged)
                    {
                        modified = true;
                        blend.BlendShape = null;
                        blend.CurBlendList = null;
                    }
                    if (blend.Renderer == null) return;
                    if (blend.CurBlendList == null)
                    {
                        var blendShapeNames = new string[blend.Renderer.sharedMesh.blendShapeCount + 1];
                        blendShapeNames[0] = "< Select Blendshape >";
                        for (int i = 0; i < blendShapeNames.Length - 1; i++)
                        {
                            blendShapeNames[i + 1] = blend.Renderer.sharedMesh.GetBlendShapeName(i);
                        }
                        blend.CurBlendList = blendShapeNames;
                        blend.CurBlendIndex = blend.BlendShape == null ? -1 :
                            blend.Renderer.sharedMesh.GetBlendShapeIndex(blend.BlendShape) + 1;
                    }
                    blend.CurBlendIndex = blend.CurBlendIndex.UpdateWith(
                        () => EditorGUI.Popup(
                                new Rect(
                                    rect.x + rect.width / 2.0f + 5, rect.y + 1, rect.width / 2.0f - 5, EditorGUIUtility.singleLineHeight),
                                blend.CurBlendIndex == -1 ? 0 : blend.CurBlendIndex,
                                blend.CurBlendList),
                        ref modified);
                    if (blend.CurBlendIndex >= 1 && blend.CurBlendIndex < blend.CurBlendList.Length)
                        blend.BlendShape = blend.CurBlendList[blend.CurBlendIndex];
                    else
                        blend.BlendShape = null;

                    var newMin = blend.StateOff;
                    var newMax = blend.StateOn;
                    EditorGUI.MinMaxSlider(new Rect(rect.x, rect.y + 3 + EditorGUIUtility.singleLineHeight, rect.width, EditorGUIUtility.singleLineHeight),
                        new GUIContent("Range", "The useable range of this blend shape. The 0-1 value of the slider will be remapped to this region between 0-100. For example, if you put the left element at half way and the right one to the end, the final slider on the avatar will turn the blendshape from 50 to 100."),
                        ref newMin, ref newMax, 0, 100
                    );
                    blend.StateOff = blend.StateOff.UpdateWith(() => newMin, ref modified);
                    blend.StateOn = blend.StateOn.UpdateWith(() => newMax, ref modified);
                };
            }

            var lblOn = AavHelpers.LblOn;
            var lblOff = AavHelpers.LblOff;

            /*
                Material Parameter Toggle
            */
            if (RLMaterialParams == null)
            {
                RLMaterialParams = new ReorderableList(this.MaterialParams, typeof(AavMaterialParamToggle), true, false, true, true);
                RLMaterialParams.elementHeight *= 2;
                RLMaterialParams.elementHeight += 2;
                RLMaterialParams.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var mparam = this.MaterialParams[index];

                    // Check for updated renderer or first time run-through and update cached material properties if necessary
                    var rendererChanged = false;
                    mparam.Renderer = mparam.Renderer.UpdateWith(
                        () => (Renderer)EditorGUI.ObjectField(
                            new Rect(rect.x, rect.y + 1, rect.width / 2.0f - 5, EditorGUIUtility.singleLineHeight),
                            mparam.Renderer, typeof(Renderer), true),
                        ref rendererChanged);
                    if (rendererChanged)
                    {
                        modified = true;
                        mparam.Property = null;
                        mparam.PropertyCache = null;
                        if (mparam.Renderer != null)
                            mparam.UpdateMaterialPropertyCache(mparam.Renderer);
                    }
                    if (mparam.Renderer == null) return;
                    if (mparam.PropertyCache == null || mparam.CachedSharedMaterials == null || !mparam.CachedSharedMaterials.SequenceEqual(mparam.Renderer.sharedMaterials))
                        mparam.UpdateMaterialPropertyCache(mparam.Renderer);

                    // Draw property selector
                    mparam.CurPropertyIndex = mparam.CurPropertyIndex.UpdateWith(
                        () => EditorGUI.Popup(
                                new Rect(
                                    rect.x + rect.width / 2.0f + 5, rect.y + 1, rect.width / 2.0f - 5, EditorGUIUtility.singleLineHeight),
                                mparam.CurPropertyIndex == -1 ? 0 : mparam.CurPropertyIndex,
                                mparam.CurPropertyList),
                        ref modified);

                    if (mparam.CurPropertyIndex >= 1 && mparam.CurPropertyIndex < mparam.CurPropertyList.Length)
                        mparam.Property = mparam.CurPropertyList[mparam.CurPropertyIndex].Substring(0, mparam.CurPropertyList[mparam.CurPropertyIndex].IndexOf(' '));
                    else
                        mparam.Property = null;

                    if (mparam.Property == null || !mparam.PropertyCache.ContainsKey(mparam.Property)) return;

                    // We have a property selected, figure out the type
                    var (propMat, propIdx) = mparam.PropertyCache[mparam.Property];

                    var isRange = false;
                    var isToggle = false;
                    mparam.Type = mparam.Type.UpdateWith(() =>
                    {
                        var attr = propMat.shader.GetPropertyAttributes(propIdx);
                        var type = propMat.shader.GetPropertyType(propIdx);
                        if (attr != null && attr.Contains("ToggleUI"))
                        {
                            isToggle = true;
                        }
                        if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
                        {
                            return AavMaterialParamType.Color;
                        }
                        else if (type == UnityEngine.Rendering.ShaderPropertyType.Range)
                        {
                            isRange = true;
                        }
                        return AavMaterialParamType.Float;
                    }, ref modified);

                    // Only float is supported here for now
                    if (mparam.Type == AavMaterialParamType.Float)
                    {
                        if (isToggle)
                        {
                            EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), "❌ Toggle property not supported for slider!");
                        }
                        else if (isRange)
                        {
                            var range = propMat.shader.GetPropertyRangeLimits(propIdx);
                            EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOn);
                            mparam.FloatValueOn = mparam.FloatValueOn.UpdateWith(
                                () => EditorGUI.Slider(
                                    new Rect(
                                        rect.x + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                                    mparam.FloatValueOn, range.x, range.y),
                                ref modified);
                            EditorGUI.LabelField(new Rect(rect.x + rect.width / 2.0f + 5, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOff);
                            mparam.FloatValueOff = mparam.FloatValueOff.UpdateWith(
                                () => EditorGUI.Slider(
                                    new Rect(
                                        rect.x + rect.width / 2.0f + 5 + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                                    mparam.FloatValueOff, range.x, range.y),
                                ref modified);
                        }
                        else
                        {
                            EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOn);
                            mparam.FloatValueOn = mparam.FloatValueOn.UpdateWith(
                                () => EditorGUI.FloatField(
                                    new Rect(
                                        rect.x + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                                    mparam.FloatValueOn),
                                ref modified);
                            EditorGUI.LabelField(new Rect(rect.x + rect.width / 2.0f + 5, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOff);
                            mparam.FloatValueOff = mparam.FloatValueOff.UpdateWith(
                                () => EditorGUI.FloatField(
                                    new Rect(
                                        rect.x + rect.width / 2.0f + 5 + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                                    mparam.FloatValueOff),
                                ref modified);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Only float properties are supported for now", MessageType.Error);
                    }
                };
            }

            GUILayout.Label("Blend Shapes affected", AavHelpers.HeaderStyle);
            RLBlendShapes.DoLayoutList();

            GUILayout.Label("Material Parameters affected", AavHelpers.HeaderStyle);
            RLMaterialParams.DoLayoutList();

            return modified;
        }

        public override void GenerateAnimator(AavGenerator gen)
        {
            var aac = gen.AAC;

            var fx = aac.CreateSupportingFxLayer(this.ParameterName);
            fx.WithAvatarMaskNoTransforms();

            var param = gen.MakeAv3Parameter(fx, this.ParameterName, this.Saved, this.Default);

            var state = fx.NewState("Updating Blend Shape")
                .MotionTime(param)
                .WithAnimation(aac.NewClip().Animating(clip =>
                {
                    foreach (var shape in this.BlendShapes)
                    {
                        clip.Animates(shape.Renderer, "blendShape." + shape.BlendShape).WithSecondsUnit(frames =>
                        {
                            frames
                                .Linear(0, shape.StateOff)
                                .Linear(1, shape.StateOn);
                        });
                    }
                    foreach (var mparam in this.MaterialParams)
                    {
                        if (mparam.Type == AavMaterialParamType.Float && mparam.Renderer != null)
                        {
                            clip.Animates(mparam.Renderer, "material." + mparam.Property).WithSecondsUnit(frames =>
                            {
                                frames
                                    .Linear(0, mparam.FloatValueOff)
                                    .Linear(1, mparam.FloatValueOn);
                            });
                        }
                    }
                }));
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            return new VRCExpressionsMenu.Control()
            {
                name = this.AavName,
                icon = this.Icon,
                // Note: "AAV" gets prefixed by "MakeAv3Parameter", so do it here too
                subParameters = new[] { new VRCExpressionsMenu.Control.Parameter() { name = "AAV" + this.ParameterName } },
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
            };
        }
    }
}
#endif