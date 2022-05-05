#if UNITY_EDITOR

using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using AnimatorAsCode.V0;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Blendshape Slider")]
    public class AavBlendShapeSlider : AavMenuItem
    {
        [Range(0, 1)]
        public float Default = 0.5f;
        public bool Saved = true;

        // Let's just be lazy and reuse this so we can also reuse the ReorderableList editor
        public List<AavToggleItem.AavBlendShapeToggle> BlendShapes = new List<AavToggleItem.AavBlendShapeToggle>();

        private ReorderableList RLBlendShapes;
        private bool modified = false;

        public override bool DrawEditor()
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
                RLBlendShapes = new ReorderableList(this.BlendShapes, typeof(AavToggleItem.AavBlendShapeToggle), true, false, true, true);
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

            GUILayout.Label("Blend Shapes affected", AavHelpers.HeaderStyle);
            RLBlendShapes.DoLayoutList();

            return modified;
        }

        public override void GenerateAnimator(AacFlBase aac, AnimatorAsVisual aav, List<string> usedAv3Parameters)
        {
            aac.RemoveAllSupportingLayers(this.ParameterName);
            var fx = aac.CreateSupportingFxLayer(this.ParameterName);
            fx.WithAvatarMaskNoTransforms();

            var param = AavGenerator.MakeAv3Parameter(aav, usedAv3Parameters, fx, this.ParameterName, this.Saved, this.Default);

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