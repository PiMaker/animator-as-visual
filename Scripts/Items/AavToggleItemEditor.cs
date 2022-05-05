#if UNITY_EDITOR

using System;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace pi.AnimatorAsVisual
{
    public partial class AavToggleItem : AavMenuItem
    {
        private ReorderableList RLToggles;
        private ReorderableList RLBlendShapes;
        private ReorderableList RLMaterialSwaps;
        private ReorderableList RLMaterialParams;

        private bool modified;

        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            modified = false;

            this.Default = this.Default.UpdateWith(() => EditorGUILayout.Toggle(
                new GUIContent("Default State", "Should this option be on when you 'Reset Avatar' in-game or when wearing the avatar for the first time."),
                    this.Default), ref modified);
            this.Saved = this.Saved.UpdateWith(() => EditorGUILayout.Toggle(
                new GUIContent("Save State", "Should this option be saved, e.g. between worlds or when you restart VRChat."),
                    this.Saved), ref modified);

            // Handle ReorderableLists
            if (RLToggles == null || RLBlendShapes == null || RLMaterialSwaps == null || RLMaterialParams == null)
            {
                /*
                    GameObject Toggle
                */
                RLToggles = new ReorderableList(this.Toggles, typeof(AavGameObjectToggle), true, false, true, true);
                RLToggles.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var toggle = this.Toggles[index];
                    toggle.Object = toggle.Object.UpdateWith(
                        () => (GameObject)EditorGUI.ObjectField(
                            new Rect(rect.x, rect.y + 1, rect.width - 110, EditorGUIUtility.singleLineHeight),
                            toggle.Object, typeof(GameObject), true),
                        ref modified);
                    EditorGUI.LabelField(new Rect(rect.x + rect.width - 100, rect.y + 1, 100, EditorGUIUtility.singleLineHeight), "Invert State");
                    toggle.Invert = toggle.Invert.UpdateWith(
                        () => EditorGUI.Toggle(
                            new Rect(rect.x + rect.width - 20, rect.y + 1, 20, EditorGUIUtility.singleLineHeight),
                            toggle.Invert),
                        ref modified);
                };

                var lblOn = AavHelpers.LblOn;
                var lblOff = AavHelpers.LblOff;

                /*
                    Blend Shape Toggle
                */
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
                    EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOn);
                    blend.StateOn = blend.StateOn.UpdateWith(
                        () => EditorGUI.Slider(
                            new Rect(
                                rect.x + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                            blend.StateOn, 0.0f, 100.0f),
                        ref modified);
                    EditorGUI.LabelField(new Rect(rect.x + rect.width / 2.0f + 5, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOff);
                    blend.StateOff = blend.StateOff.UpdateWith(
                        () => EditorGUI.Slider(
                            new Rect(
                                rect.x + rect.width / 2.0f + 5 + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 30, EditorGUIUtility.singleLineHeight),
                            blend.StateOff, 0.0f, 100.0f),
                        ref modified);
                };

                /*
                    Material Parameter Toggle
                */
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

                    // Draw correct On/Off selection UI based on type
                    if (mparam.Type == AavMaterialParamType.Float)
                    {
                        if (isToggle)
                        {
                            EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 100, EditorGUIUtility.singleLineHeight), "Invert Property");
                            mparam.FloatValueOn = mparam.FloatValueOn.UpdateWith(
                                () => EditorGUI.Toggle(
                                    new Rect(
                                        rect.x + 105, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight),
                                    mparam.FloatValueOn < 0.5f) ? 0.0f : 1.0f,
                                ref modified);
                            mparam.FloatValueOff = mparam.FloatValueOff.UpdateWith(() => mparam.FloatValueOn > 0.5f ? 0.0f : 1.0f, ref modified);
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
                    else if (mparam.Type == AavMaterialParamType.Color)
                    {
                        // Unfortunately the "[HDR]" shader attribute doesn't appear to be exposed, so user must select instead
                        EditorGUI.LabelField(new Rect(rect.x + rect.width - 53, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), "HDR");
                        mparam.ColorIsHDR = mparam.ColorIsHDR.UpdateWith(
                            () => EditorGUI.Toggle(
                                new Rect(rect.x + rect.width - 20, rect.y + 4 + EditorGUIUtility.singleLineHeight, 20, EditorGUIUtility.singleLineHeight),
                                mparam.ColorIsHDR),
                            ref modified);

                        EditorGUI.LabelField(new Rect(rect.x, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOn);
                        mparam.ColorValueOn = mparam.ColorValueOn.UpdateWith(
                            () => EditorGUI.ColorField(
                                new Rect(
                                    rect.x + 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 60, EditorGUIUtility.singleLineHeight),
                                new GUIContent(""), mparam.ColorValueOn, true, true, mparam.ColorIsHDR),
                            ref modified);
                        EditorGUI.LabelField(new Rect(rect.x + rect.width / 2.0f + 5 - 30, rect.y + 4 + EditorGUIUtility.singleLineHeight, 30, EditorGUIUtility.singleLineHeight), lblOff);
                        mparam.ColorValueOff = mparam.ColorValueOff.UpdateWith(
                            () => EditorGUI.ColorField(
                                new Rect(
                                    rect.x + rect.width / 2.0f + 5, rect.y + 4 + EditorGUIUtility.singleLineHeight, rect.width / 2.0f - 5 - 60, EditorGUIUtility.singleLineHeight),
                                new GUIContent(""), mparam.ColorValueOff, true, true, mparam.ColorIsHDR),
                            ref modified);
                    }
                };

                /*
                    Material Swaps
                */

                // WIP

                RLMaterialSwaps = new ReorderableList(this.MaterialSwaps, typeof(AavMaterialSwapToggle), true, false, true, true);
                RLMaterialSwaps.elementHeight *= 2;
                RLMaterialSwaps.elementHeight += 2;
                RLMaterialSwaps.drawElementCallback += (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    EditorGUI.LabelField(rect, "// Work in Progress!");
                };
            }

            var headerStyle = AavHelpers.HeaderStyle;

            // Render lists
            GUILayout.Label("GameObject Toggles", headerStyle);
            RLToggles.DoLayoutList();
            GUILayout.Label("Blend Shapes", headerStyle);
            RLBlendShapes.DoLayoutList();
            GUILayout.Label("Material Parameters", headerStyle);
            RLMaterialParams.DoLayoutList();
            GUILayout.Label("Material Swaps", headerStyle);
            RLMaterialSwaps.DoLayoutList();

            return modified;
        }
    }
}
#endif