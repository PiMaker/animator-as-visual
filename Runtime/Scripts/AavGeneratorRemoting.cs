#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using AnimatorAsCode.Pi.V0;
using VRC.SDK3.Dynamics.Contact.Components;

namespace pi.AnimatorAsVisual
{
    public partial class AavGenerator
    {
        private GameObject remotingRoot;

        private AavRemotingItem.RemotingDataNode GenerateRemotingData(AavSubmenuItem root, bool isRoot = true)
        {
            if (root == null || root.Items.Count() == 0)
                return default;

            var ret = new AavRemotingItem.RemotingDataNode()
            {
                IsFolder = true,
                Name = isRoot ? AAV.Avatar.name : root.AavName,
            };

            var currentChildList = new List<AavRemotingItem.RemotingDataNode>();

            foreach (var item in root.Items)
            {
                if (item == null) continue;
                if (item is AavSubmenuItem sub)
                {
                    var gen = GenerateRemotingData(sub, isRoot: false);
                    if (gen.Name != null && gen.Children != null && gen.Children.Length > 0)
                        currentChildList.Add(gen);
                }
                else if (item is AavToggleItem toggle && toggle.AllowRemoteToggle && toggle.isActiveAndEnabled)
                {
                    currentChildList.Add(new AavRemotingItem.RemotingDataNode()
                    {
                        IsFolder = false,
                        Name = toggle.AavName,
                        ParameterName = toggle.ParameterName,
                    });
                }
            }

            ret.Children = currentChildList.ToArray();

            // avoid creating unnecessary folders
            if (ret.Children.Length == 1 && ret.Children[0].IsFolder)
            {
                ret = ret.Children[0];
                if (isRoot)
                    ret.Name = AAV.Avatar.name;
            }

            return ret;
        }

        private void GenerateRemotingReceivers()
        {
            var toggles = new List<AavToggleItem>();
            foreach (var item in AAV.Root.EnumerateRecursive())
            {
                if (item is AavToggleItem toggle && toggle.AllowRemoteToggle && toggle.isActiveAndEnabled)
                    toggles.Add(toggle);
            }

            if (toggles.Count == 0)
                return;

            remotingRoot = AAV.Avatar.transform.Find("AAV-Remoting-Root")?.gameObject;
            if (remotingRoot != null)
                GameObject.DestroyImmediate(remotingRoot);
            remotingRoot = new GameObject("AAV-Remoting-Root");
            remotingRoot.transform.SetParent(AAV.Avatar.transform);
            remotingRoot.transform.localPosition = Vector3.zero;

            var fx = AAC.CreateSupportingFxLayer("AAV-Remoting-Receiver");
            fx.WithAvatarMaskNoTransforms();

            var detectLocalState = fx.NewState("DetectLocal", -2, 0);

            var emptyState = fx.NewState("Empty", 0, 0);
            var receiverPos = 0;

            detectLocalState.TransitionsTo(emptyState).When(fx.Av3().ItIsLocal());

            foreach (var toggle in toggles)
            {
                var paramName = "RemoteAAV-RCV-" + toggle.ParameterName;
                var receiver = remotingRoot.AddComponent<VRCContactReceiver>();

                receiver.collisionTags = new List<string>() { "AAV-Contact-" + toggle.ParameterName };
                receiver.shapeType = VRC.Dynamics.ContactBase.ShapeType.Sphere;
                receiver.radius = 0.01f;
                receiver.allowOthers = true;
                receiver.allowSelf = true; // do we want this? idk, but we have it now
                receiver.localOnly = true;
                receiver.collisionValue = 1.0f;
                receiver.paramValue = 1.0f;
                receiver.parameter = paramName;
                receiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Constant;
                receiver.minVelocity = 0.0f;
                receiver.enabled = true;

                var contactParam = fx.FloatParameter(paramName);
                var stateToOn = fx.NewState("RCV-to-on-" + toggle.ParameterName, 2, receiverPos++);
                var stateToOff = fx.NewState("RCV-to-off-" + toggle.ParameterName, 2, receiverPos++);
                if (toggle.CanUseBlendTree)
                {
                    var param = fx.FloatParameter("AAV" + toggle.ParameterName);
                    stateToOn.Drives(param, 1.0f).DrivingLocally();
                    stateToOff.Drives(param, 0.0f).DrivingLocally();

                    emptyState.TransitionsTo(stateToOn).When(contactParam.IsGreaterThan(0.5f)).And(param.IsLessThan(0.5f));
                    emptyState.TransitionsTo(stateToOff).When(contactParam.IsGreaterThan(0.5f)).And(param.IsGreaterThan(0.5f));
                }
                else
                {
                    var param = fx.BoolParameter("AAV" + toggle.ParameterName);
                    stateToOn.Drives(param, true).DrivingLocally();
                    stateToOff.Drives(param, false).DrivingLocally();

                    emptyState.TransitionsTo(stateToOn).When(contactParam.IsGreaterThan(0.5f)).And(param.IsFalse());
                    emptyState.TransitionsTo(stateToOff).When(contactParam.IsGreaterThan(0.5f)).And(param.IsTrue());
                }

                stateToOn.TransitionsTo(emptyState).When(contactParam.IsLessThan(0.5f));
                stateToOff.TransitionsTo(emptyState).When(contactParam.IsLessThan(0.5f));
            }
        }

        private void GenerateRemotingSenders(AacFlLayer fx)
        {
            var remotes = new List<AavRemotingItem>();
            foreach (var item in AAV.Root.EnumerateRecursive())
            {
                if (item is AavRemotingItem remote)
                    remotes.Add(remote);
            }

            if (remotes.Count == 0)
                return;

            if (remotingRoot == null)
            {
                remotingRoot = new GameObject("AAV-Remoting-Root");
                remotingRoot.transform.SetParent(AAV.Avatar.transform);
                remotingRoot.transform.localPosition = Vector3.zero;
            }

            foreach (var tag in remotes.SelectMany(r => r.ContactSenderTags))
            {
                var go = new GameObject("Sender-" + tag);
                go.transform.SetParent(remotingRoot.transform);
                var sender = go.AddComponent<VRCContactSender>();
                sender.collisionTags = new List<string>() { tag };
                sender.shapeType = VRC.Dynamics.ContactBase.ShapeType.Sphere;
                sender.radius = 1_000_000;
                sender.enabled = true;
                go.SetActive(false);

                var clipOn = AAC.NewClip().Toggling(go, true);
                var clipOff = AAC.NewClip().Toggling(go, false);

                var triggerParamName = "TriggerSend-" + tag;
                blendTreeMotions.Add((clipOn.Clip, clipOff.Clip, MakeAv3ParameterBoolFloat(fx, triggerParamName, false, false)));
            }
        }
    }
}
#endif