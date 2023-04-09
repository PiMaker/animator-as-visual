#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V0;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Remoting Toggle")]
    public class AavRemotingItem : AavMenuItem
    {
        public string TargetParameterName;

        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            var my = new SerializedObject(this);
            my.Update();

            var parameterName = my.FindProperty(nameof(TargetParameterName));
            EditorGUILayout.PropertyField(parameterName);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Set from:");

            var obj = EditorGUILayout.ObjectField((UnityEngine.Object)null, typeof(AavToggleItem), true);
            if (obj != null && obj is AavToggleItem fromItem)
            {
                parameterName.stringValue = "AAV" + fromItem.ParameterName;
            }

            my.ApplyModifiedProperties();

            return false;
        }

        public override void GenerateAnimator(AavGenerator gen)
        {
            var aac = gen.AAC;
            var aav = gen.AAV;

            if (string.IsNullOrEmpty(this.TargetParameterName))
            {
                Debug.LogError("Remoting Item without target parameter found: " + this.AavName);
                return;            
            }

            var root = aav.Avatar.transform.Find("AAV-Remoting-Root")?.gameObject;
            if (root == null)
            {
                root = new GameObject("AAV-Remoting-Root");
                root.transform.SetParent(aav.Avatar.transform);
            }

            root.transform.position = aav.Avatar.transform.position;

            var element = root.transform.Find(this.TargetParameterName)?.gameObject;
            if (element == null)
            {
                element = new GameObject(this.TargetParameterName);
                element.transform.SetParent(root.transform);
            }

            element.transform.localPosition = Vector3.zero;

            var sender = element.GetComponent<VRCContactSender>();
            if (sender == null)
            {
                sender = element.AddComponent<VRCContactSender>();
            }
            
            var receiver = element.GetComponent<VRCContactReceiver>();
            if (receiver == null)
            {
                receiver = element.AddComponent<VRCContactReceiver>();
            }

            sender.collisionTags = new List<string>() { this.TargetParameterName };
            sender.shapeType = VRC.Dynamics.ContactBase.ShapeType.Sphere;
            sender.radius = 1000000;
            sender.enabled = false;

            receiver.collisionTags = new List<string>() { this.TargetParameterName };
            receiver.shapeType = VRC.Dynamics.ContactBase.ShapeType.Sphere;
            receiver.radius = 0.1f;
            receiver.allowOthers = true;
            receiver.allowSelf = false;
            receiver.localOnly = true;
            receiver.collisionValue = 1.0f;
            receiver.paramValue = 1.0f;
            receiver.parameter = "RemoteAAV-RCV-" + this.TargetParameterName;
            receiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Constant;
            receiver.enabled = true;

            aac.RemoveAllSupportingLayers(this.ParameterName);
            var fx = aac.CreateSupportingFxLayer(this.ParameterName);
            fx.WithAvatarMaskNoTransforms();

            var triggerParam = gen.MakeAv3Parameter(fx, "RemoteAAV-" + this.TargetParameterName, false, false);
            var rcvParam = fx.BoolParameter("RemoteAAV-RCV-" + this.TargetParameterName);
            var targetParam = fx.BoolParameter(this.TargetParameterName);

            var waitingOnTrigger = fx.NewState("WaitingOnTrigger").WithAnimation(aac.NewClip().TogglingComponent(sender, false));

            var triggered = fx.NewState("Triggered").WithAnimation(aac.NewClip().TogglingComponent(sender, true));
            waitingOnTrigger.TransitionsTo(triggered).When(triggerParam.IsTrue());
            triggered.TransitionsTo(waitingOnTrigger).When(triggerParam.IsFalse());

            var rcvWhileOff = fx.NewState("ReceivedWhileOff").Drives(targetParam, true);
            var rcvWhileOn = fx.NewState("ReceivedWhileOn").Drives(targetParam, false);

            waitingOnTrigger.TransitionsTo(rcvWhileOff).When(rcvParam.IsTrue()).And(targetParam.IsFalse()).And(fx.Av3().ItIsLocal());
            waitingOnTrigger.TransitionsTo(rcvWhileOn).When(rcvParam.IsTrue()).And(targetParam.IsTrue()).And(fx.Av3().ItIsLocal());
            rcvWhileOff.TransitionsTo(waitingOnTrigger).When(rcvParam.IsFalse());
            rcvWhileOn.TransitionsTo(waitingOnTrigger).When(rcvParam.IsFalse());
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            return new VRCExpressionsMenu.Control()
            {
                icon = this.Icon,
                name = this.AavName,
                parameter = new VRCExpressionsMenu.Control.Parameter() { name = "AAVRemoteAAV-" + this.TargetParameterName },
                type = VRCExpressionsMenu.Control.ControlType.Button,
            };
        }

        [MenuItem("Tools/Animator As Visual/Clone Menu As Remoting")]
        public static void ConstructRemotingClone()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0) return;

            var folderComp = objs[0].GetComponent<AavSubmenuItem>();
            if (folderComp == null)
            {
                EditorUtility.DisplayDialog("Error", "You must select an AAV submenu item!", "Ok");
                return;
            }

            var clone = Instantiate<GameObject>(folderComp.gameObject);
            clone.transform.SetParent(folderComp.transform.parent);
            var cloneComp = clone.GetComponent<AavSubmenuItem>();

            void ChangeToRemotingItem(AavMenuItem item)
            {
                if (item is AavSubmenuItem sub)
                {
                    foreach (var item2 in sub.Items)
                    {
                        ChangeToRemotingItem(item2);
                    }
                    return;
                }
                else if (item is AavToggleItem)
                {
                    var newRemote = item.gameObject.AddComponent<AavRemotingItem>();
                    newRemote.AavName = item.AavName;
                    newRemote.Icon = item.Icon;
                    newRemote.TargetParameterName = "AAV" + item.ParameterName;
                }

                if (item.gameObject.GetComponents<AavMenuItem>().Length == 1)
                {
                    GameObject.DestroyImmediate(item.gameObject);
                }
                else
                {
                    Component.DestroyImmediate(item);
                }
            }

            ChangeToRemotingItem(cloneComp);
        }
    }
}

#endif