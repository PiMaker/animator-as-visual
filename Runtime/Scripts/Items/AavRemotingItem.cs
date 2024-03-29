#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace pi.AnimatorAsVisual
{
    [Serializable]
    [AavMenu("Remote Control")]
    public class AavRemotingItem : AavMenuItem
    {
        public RemotingDataNode RemotingData;
        private string editorRemotingDataString;
        private readonly static StringBuilder tmpBuilder = new StringBuilder();

        private static Texture2D folderIcon;

        [Serializable]
        public struct RemotingDataNode
        {
            public string Name;
            public bool IsFolder;
            public bool IsButton;
            public string ParameterName;
            public RemotingDataNode[] Children;
        }

        public IEnumerable<string> ContactSenderTags
        {
            get
            {
                if (RemotingData.Name == null || RemotingData.Children == null)
                    return Enumerable.Empty<string>();

                IEnumerable<string> GetRecursive(RemotingDataNode node)
                {
                    if (node.Name == null || node.Children == null)
                        yield break;

                    if (node.IsFolder)
                    {
                        foreach (var child in node.Children)
                        {
                            foreach (var tag in GetRecursive(child))
                                yield return tag;
                        }
                    }
                    else
                    {
                        yield return "AAV-Contact-" + node.ParameterName;
                    }
                }

                return GetRecursive(RemotingData);
            }
        }

        public override bool DrawEditor(AnimatorAsVisual aav)
        {
            var my = new SerializedObject(this);
            my.Update();

            editorRemotingDataString = EditorGUILayout.TextField("Remoting Data", editorRemotingDataString);
            if (GUILayout.Button("Load Remoting Data"))
                RemotingData = JsonUtility.FromJson<RemotingDataNode>(editorRemotingDataString);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField($"Controls avatar: {RemotingData.Name ?? "<Not Loaded>"}");

            void ShowRecursive(StringBuilder builder, RemotingDataNode node, int indent = 0)
            {
                if (node.Name == null || node.Children == null) return;

                builder.Append(' ', indent * 2);
                builder.Append("- ");
                builder.Append(node.Name);
                if (!node.IsFolder)
                {
                    builder.Append(" (");
                    builder.Append(node.ParameterName);
                    builder.AppendLine(")");
                }
                else
                {
                    builder.AppendLine();
                    foreach (var child in node.Children)
                    {
                        ShowRecursive(builder, child, indent + 1);
                    }
                }
            }
            if (RemotingData.Name != null)
            {
                tmpBuilder.Clear();
                ShowRecursive(tmpBuilder, RemotingData);
                EditorGUILayout.HelpBox(tmpBuilder.ToString(), MessageType.None);
            }

            my.ApplyModifiedProperties();

            return false;
        }

        public override void GenerateAnimator(AavGenerator gen)
        {
            // taken care of for us by AavGenerator special handling
        }

        public override VRCExpressionsMenu.Control GenerateAv3MenuEntry(AnimatorAsVisual aav)
        {
            if (folderIcon == null)
                folderIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/item_folder.png");

            VRCExpressionsMenu.Control AddLayer(RemotingDataNode node, string name, bool noFolderIcon = false)
            {
                if (node.Name == null || node.Children == null) return null;

                var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                subMenu.name = name;
                AssetDatabase.AddObjectToAsset(subMenu, AssetDatabase.GetAssetPath(aav.Avatar.expressionsMenu));
                foreach (var child in node.Children)
                {
                    if (child.IsFolder)
                    {
                        var sub = AddLayer(child, child.Name);
                        if (sub != null)
                            subMenu.controls.Add(sub);
                    }
                    else
                    {
                        subMenu.controls.Add(new VRCExpressionsMenu.Control()
                        {
                            name = child.Name,
                            icon = this.Icon,
                            parameter = new VRCExpressionsMenu.Control.Parameter() { name = "AAVTriggerSend-AAV-Contact-" + child.ParameterName },
                            type = VRCExpressionsMenu.Control.ControlType.Button,
                        });
                    }
                }

                return new VRCExpressionsMenu.Control()
                {
                    name = name,
                    icon = noFolderIcon || folderIcon == null ? this.Icon : folderIcon,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = subMenu,
                };
            }

            return AddLayer(RemotingData, this.AavName, noFolderIcon: true);
        }
    }
}

#endif