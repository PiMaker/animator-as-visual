#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Raw Av3 Menu Entry")]
    public class AavRawMenuItem : AavMenuItem
    {
        public VRCExpressionsMenu.Control.Label[] RawLabels;
        public string RawParameterName;
        public VRCExpressionsMenu.Control.ControlType RawType = VRCExpressionsMenu.Control.ControlType.Toggle;
        public float RawValue;
        public VRCExpressionsMenu.Control.Style RawStyle;
        public VRCExpressionsMenu RawSubMenu;
        public string[] RawSubParameters;

        private bool expertFoldout;

        public void SetDataFromControl(VRCExpressionsMenu.Control baseControl)
        {
            RawLabels = baseControl.labels;
            RawParameterName = baseControl.parameter.name;
            RawType = baseControl.type;
            RawValue = baseControl.value;
            RawStyle = baseControl.style;
            RawSubMenu = baseControl.subMenu;
            RawSubParameters = baseControl.subParameters.Select(x => x.name).ToArray();
            Icon = baseControl.icon;
        }

        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            var my = new SerializedObject(this);
            my.Update();

            var parameterName = my.FindProperty(nameof(RawParameterName));
            var type = my.FindProperty(nameof(RawType));
            var value = my.FindProperty(nameof(RawValue));
            var subMenu = my.FindProperty(nameof(RawSubMenu));
            var subParameters = my.FindProperty(nameof(RawSubParameters));

            EditorGUILayout.PropertyField(type);
            
            var parameters = aav.Avatar?.expressionParameters?.parameters;
            VRCExpressionParameters.Parameter selectedParameter = null;
            if (parameters != null)
            {
                var allParameters = parameters.Select(x => x.name).Concat(new[]{"< None >"}).ToArray();
                var currentIndex = parameterName.stringValue == "" ? parameters.Length : (parameterName.stringValue.Length > 0 ? Array.IndexOf(allParameters, parameterName.stringValue) : 0);
                if (currentIndex < 0)
                {
                    EditorGUILayout.PropertyField(parameterName);
                }
                else
                {
                    var newIndex = EditorGUILayout.Popup("Parameter", currentIndex, allParameters);
                    if (newIndex >= 0)
                    {
                        if (newIndex >= 0 && newIndex < parameters.Length)
                        {
                            selectedParameter = parameters[newIndex];
                            parameterName.stringValue = allParameters[newIndex];
                        }
                        else
                        {
                            selectedParameter = null;
                            parameterName.stringValue = "";
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.PropertyField(parameterName);
            }

            if (selectedParameter != null)
            {
                switch (selectedParameter.valueType)
                {
                    case VRCExpressionParameters.ValueType.Int:
                        value.floatValue = EditorGUILayout.IntField("Value", (int)value.floatValue);
                        break;
                    case VRCExpressionParameters.ValueType.Float:
                        value.floatValue = EditorGUILayout.FloatField("Value", value.floatValue);
                        break;
                    case VRCExpressionParameters.ValueType.Bool:
                        value.floatValue = EditorGUILayout.Toggle("Value", value.floatValue > 0) ? 1.0f : 0.0f;
                        break;
                    default:
                        EditorGUILayout.PropertyField(value);
                        break;
                }
            }

            if (RawType == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                EditorGUILayout.PropertyField(subMenu);
            }
            EditorGUILayout.PropertyField(subParameters);

            if (expertFoldout = EditorGUILayout.Foldout(expertFoldout, "Hidden Settings"))
            {
                var labels = my.FindProperty(nameof(RawLabels));
                var style = my.FindProperty(nameof(RawStyle));

                EditorGUILayout.PropertyField(labels);
                EditorGUILayout.PropertyField(style);
            }

            my.ApplyModifiedProperties();

            return false;
        }

        public override void GenerateAnimator(AavGenerator gen)
        {
            // ignored
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            return new VRCExpressionsMenu.Control()
            {
                icon = this.Icon,
                name = this.AavName,
                labels = this.RawLabels,
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = this.RawParameterName },
                type = this.RawType,
                value = this.RawValue,
                style = this.RawStyle,
                subMenu = this.RawSubMenu,
                subParameters = this.RawSubParameters == null ?
                    new VRCExpressionsMenu.Control.Parameter[0] :
                    this.RawSubParameters
                        .Select(x => new VRCExpressionsMenu.Control.Parameter() { name = x })
                        .ToArray(),
            };
        }
    }
}

#endif