#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

using GestureManager.Scripts.Core.Editor;
using GestureManager.Scripts.Editor.Modules.Vrc3;
using GestureManager.Scripts.Editor.Modules.Vrc3.RadialButtons;

using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.IO;

namespace pi.AnimatorAsVisual
{
    [CustomEditor(typeof(AnimatorAsVisual))]
    public class AavEditor : Editor
    {
        private const string GeneratedFolder = "Generated-AAV";

        private const float Size = 300.0f;
        private const float InnerSize = Size / 3;
        private const float Clamp = Size / 3;
        private const float ClampReset = Size / 1.7f;

        private AnimatorAsVisual data = null;
        internal AnimatorAsVisual Data
        {
            get => data ?? target as AnimatorAsVisual;
            set => data = value;
        }
        public override bool RequiresConstantRepaint() => true; // FIXME? Performance?

        private bool modified = false;
        private bool expertFoldoutOpen = false;
        private bool importFoldoutOpen = false;

        private AavSubmenuItem _currentMenu;
        private AavSubmenuItem CurrentMenu
        {
            get
            {
                if (_currentMenu == null)
                {
                    if (Data.CurrentMenu == null)
                    {
                        if (Data.Root == null)
                        {
                            var go = new GameObject("Root");
                            Data.Root = go.AddComponent<AavSubmenuItem>();
                            Data.Root.transform.parent = Data.transform;
                        }
                        Data.CurrentMenu = Data.Root;
                    }
                    _currentMenu = Data.CurrentMenu;
                }
                return _currentMenu;
            }
            set
            {
                Data.CurrentMenu = _currentMenu = value;
            }
        }

        public AavEditor(AnimatorAsVisual aav)
        {
            data = aav;
        }

        /*
            VisualElements containing the GestureManager dial UI -
            this and accompanying code is very much taken and adapted from GestureManager itself.
        */
        private VisualElement root;

        private RadialCursor cursor;
        private VisualElement borderHolder;
        private VisualElement sliceHolder;
        private VisualElement dataHolder;
        private VisualElement puppetHolder;
        private VisualElement radial;

        private RadialMenuItem[] buttons;
        private List<GmgButton> selectionTuple;

        /*
            Resource loading and initialization
        */
        private Texture2D iconPlus;

        private void LoadResources()
        {
            iconPlus = Resources.Load<Texture2D>("aac_visual_plus");
        }

        public void OnEnable()
        {
            radial = RadialMenuUtility.Prefabs.NewCircle(Size, RadialMenuUtility.Colors.RadialCenter, RadialMenuUtility.Colors.RadialCenter, RadialMenuUtility.Colors.CustomBorder);
            // overlap with imgui rectangle to get correct input events
            radial.style.top = -Size;
            radial.style.marginBottom = -Size;
            radial.style.alignSelf = Align.Center;

            sliceHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });
            borderHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });
            radial.MyAdd(RadialMenuUtility.Prefabs.NewCircle((int)InnerSize, RadialMenuUtility.Colors.RadialInner, RadialMenuUtility.Colors.CustomBorder, Position.Absolute));

            dataHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });
            puppetHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });

            cursor = new RadialCursor();
            radial.MyAdd(cursor);
            cursor.SetData(Clamp, ClampReset, (int)(InnerSize / 2f), (int)(Size / 2f), radial);

            LoadResources();

            EditorApplication.delayCall += () => {
                if (!PrefabUtility.IsPartOfPrefabAsset(Data.gameObject) && data == null)
                {
                    // probably editing AAV root component
                    CurrentMenu = Data.Root;
                    Data.CurrentlySelected = -1;
                }
                GenerateMenu();
            };
        }

        /*
            GUI and input handling
            Heavily adapted from GestureManager
        */

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (PrefabUtility.IsPartOfPrefabAsset(Data.gameObject))
            {
                root.Add(new IMGUIContainer(() => GUILayout.Label("Please put this Prefab into your Scene!")));
                return root;
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(Data.gameObject))
            {
                // unpack to avoid serialization issues
                PrefabUtility.UnpackPrefabInstance(Data.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                root.Add(new IMGUIContainer(() => GUILayout.Label("You cannot edit your Av3 Menu in Play Mode!")));
                return root;
            }

            if (Data != null && CurrentMenu != null)
            {
                root.Add(new IMGUIContainer(DrawSyncButton));

                // this will allocate an empty rectangle to receive input events
                root.Add(new IMGUIContainer(DrawInputHandler));

                root.Add(radial);
                root.Add(new IMGUIContainer(DrawEntryEditor));
            }

            return root;
        }

        public void DrawInputHandler()
        {
            // This is *required* to receive input events, as well as just being heading - Unity dumb
            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize += 4;
            headerStyle.fixedHeight = 40;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("Av3 Menu Editor", headerStyle);

            var rect = GUILayoutUtility.GetRect(new GUIContent(), GUIStyle.none,
                                                GUILayout.ExpandWidth(true), GUILayout.Height(Size));
            if (Event.current.type == EventType.Layout) return;
            var pos = Event.current.mousePosition - rect.center;
            if (Event.current.type == EventType.MouseDown) OnClickStart(pos);
            if (Event.current.type == EventType.MouseUp) OnClickEnd(pos);
            if (selectionTuple != null) cursor.Update(pos, selectionTuple, false);
        }

        private void OnClickStart(Vector2 pos)
        {
            var choice = cursor.GetChoice(buttons.Length, false);
            if (choice != -1 && buttons != null) buttons[choice].OnClickStart();
        }

        private void OnClickEnd(Vector2 pos)
        {
            var choice = cursor.GetChoice(buttons.Length, false);
            if (choice != -1 && buttons != null) buttons[choice].OnClickEnd();
        }

        private void SetButtons(RadialMenuItem[] buttons)
        {
            this.buttons = buttons;

            borderHolder.Clear();
            sliceHolder.Clear();
            dataHolder.Clear();

            var step = 360f / this.buttons.Length;
            var current = -step / 2;
            var progress = 1f / this.buttons.Length;

            var rStep = Mathf.PI * 2 / this.buttons.Length;
            var rCurrent = Mathf.PI;

            selectionTuple = new List<GmgButton>();

            foreach (var item in this.buttons)
            {
                item.Create();
                //borderHolder.MyAdd(item.Border).transform.rotation = Quaternion.Euler(0, 0, current);
                var circle = RadialMenuUtility.Prefabs.NewSlice(Size, RadialMenuUtility.Colors.RadialCenter, RadialMenuUtility.Colors.CustomMain, RadialMenuUtility.Colors.CustomBorder);
                var circleHolder = new VisualElement();
                circleHolder.Add(circle);
                circle.Progress = progress;

                item.DataHolder.transform.position = new Vector3(Mathf.Sin(rCurrent) * Size / 3, Mathf.Cos(rCurrent) * Size / 3, 0);
                sliceHolder.MyAdd(circleHolder).transform.rotation = Quaternion.Euler(0, 0, current);
                borderHolder.MyAdd(RadialMenuUtility.Prefabs.NewBorder(Size / 2)).transform.rotation = Quaternion.Euler(0, 0, current - 90);

                // highlight selection
                if (Data.CurrentlySelected != -1 && item == this.buttons[Data.CurrentlySelected + 1])
                {
                    foreach (var ve in item.DataHolder.Children())
                    {
                        //ve.style.color = Color.magenta;
                        ve.style.backgroundColor = Color.blue;
                        ve.style.fontSize = 16;
                    }
                }

                dataHolder.MyAdd(item.DataHolder);
                selectionTuple.Add(new GmgButton() { Button = item, Data = item.DataHolder, CircleElement = circle });
                current += step;
                rCurrent -= rStep;
            }

            cursor.Selection = cursor.GetChoice(buttons.Length, false);
            if (cursor.Selection != -1) RadialCursor.Sel(selectionTuple[cursor.Selection], true);
        }

        /*
            Visual editor functionality
        */

        internal void GenerateMenu()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(Data.gameObject)) return;
            if (Data.CurrentlySelected >= CurrentMenu.Items.Count()) Data.CurrentlySelected = -1;

            var list = new List<RadialMenuItem>();
            list.Add(new RadialMenuButton(HandleAddControl, "Add Control", iconPlus));

            var i = 0;
            foreach (var item in CurrentMenu.Items)
            {
                var isSubmenu = item is AavSubmenuItem;
                var label = (isSubmenu ? "[M] " : "") + item.AavName;
                list.Add(new RadialMenuButton(HandleEntrySelected(i++), label, item.Icon));
            }

            SetButtons(list.ToArray());

            AddTypeSelectedHandlers();
        }

        // curry the index into an Action
        // mmmh curry
        private Action HandleEntrySelected(int index)
        {
            return () =>
            {
                var entry = CurrentMenu.Items.ElementAt(index);
                if (Data.CurrentlySelected == index)
                {
                    Data.CurrentlySelected = -1;
                }
                else
                {
                    Data.CurrentlySelected = index;
                    Selection.SetActiveObjectWithContext(CurrentMenu.transform.GetChild(Data.CurrentlySelected), null);
                }
                GenerateMenu();
            };
        }

        private void AddTypeSelectedHandlers()
        {
            foreach (var item in CurrentMenu.Items)
            {
                var tsi = item as AavTypeSelectorItem;
                if (tsi != null)
                {
                    tsi.TypeSelected = HandleControlTypeSelected;
                }
            }
        }

        private void HandleAddControl()
        {
            var go = new GameObject("New Entry");
            var selectorItem = go.AddComponent<AavTypeSelectorItem>();
            selectorItem.transform.parent = Data.CurrentMenu.transform;
            AddTypeSelectedHandlers();

            Data.Dirty = true;
            Selection.SetActiveObjectWithContext(go, null);
            EditorGUIUtility.PingObject(go);

            GenerateMenu();
        }

        private void HandleControlTypeSelected(AavTypeSelectorItem sender, Type type)
        {
            var newEntry = (AavMenuItem)sender.gameObject.AddComponent(type);
            newEntry.Icon = sender.Icon;

            DestroyImmediate(sender);
            GenerateMenu();
        }

        private void OpenSubmenu(AavSubmenuItem menu)
        {
            CurrentMenu = menu;
            Data.CurrentlySelected = -1;
            Selection.SetActiveObjectWithContext(CurrentMenu, null);
            EditorApplication.delayCall += GenerateMenu;
        }

        /*
            Editor drawing logic
        */
        private void DrawEntryEditor()
        {
            GUILayout.Space(14);

            if (CurrentMenu != null && CurrentMenu.gameObject == null)
            {
                EditorGUILayout.HelpBox("The current menu structure is out of date/invalid. Please delete your AnimatorAsVisual object and place a new instance of the Prefab in your scene.", MessageType.Error);
                return;
            }

            // Draw Breadcrumbs
            var path = new StringBuilder(CurrentMenu.AavName);
            var p = CurrentMenu.Parent;
            while (p != null)
            {
                path.Insert(0, " > ");
                path.Insert(0, p.AavName);
                p = p.Parent;
            }
            GUILayout.Label("Menu Path: " + path);

            // Draw Menu Up button
            if (CurrentMenu.Parent != null && GUILayout.Button("^ Up ^", AavHelpers.BigButtonStyle))
            {
                var prev = CurrentMenu;
                CurrentMenu = CurrentMenu.Parent;
                Data.CurrentlySelected = -1;
                Selection.SetActiveObjectWithContext(CurrentMenu, null);
                return;
            }

            GUILayout.Space(10);
            GmgLayoutHelper.Divisor(1);
            GUILayout.Space(10);

            var currentItemCount = CurrentMenu.Items.Count();

            if (Data.CurrentlySelected >= currentItemCount) Data.CurrentlySelected = -1;
            if (Data.CurrentlySelected == -1)
            {
                GUILayout.Label("Please select an entry to edit.");
                GUILayout.Space(10);
                return;
            }

            modified = false;
            var entry = CurrentMenu.Items.ElementAt(Data.CurrentlySelected);

            Undo.RecordObject(entry, "Animator As Visual");

            // Draw basic editor fields
            var headerStyle = AavHelpers.HeaderStyle;
            GUILayout.Label(entry.GUIName, headerStyle);

            entry.AavName = entry.AavName.UpdateWith(() => EditorGUILayout.TextField("Entry Name", entry.AavName), ref modified);
            GUILayout.Space(4);
            entry.Icon = entry.Icon.UpdateWith(() => (Texture2D)EditorGUILayout.ObjectField("Menu Icon", entry.Icon, typeof(Texture2D), false), ref modified);
            GUILayout.Space(4);

            // Draw the editor for the currently selected entry now
            modified |= entry.DrawEditor(Data);

            // Big red delete button
            GUILayout.Space(16);
            var resetCol = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Entry", AavHelpers.BigButtonStyle))
            {
                //CurrentMenu.Items.RemoveAt(Data.CurrentlySelected);
                DestroyImmediate(entry.gameObject);
                Data.CurrentlySelected--;
                if (Data.CurrentlySelected == -1)
                {
                    Selection.SetActiveObjectWithContext(CurrentMenu.transform, null);
                }
                else if (CurrentMenu.Items.ElementAt(Data.CurrentlySelected) is AavSubmenuItem)
                {
                    Data.CurrentlySelected = -1;
                    Selection.SetActiveObjectWithContext(CurrentMenu.transform, null);
                }
                else
                {
                    Selection.SetActiveObjectWithContext(CurrentMenu.transform.GetChild(Data.CurrentlySelected), null);
                }
                Data.Dirty = true;
                EditorApplication.delayCall += GenerateMenu;
                return;
            }
            if (!(entry is AavTypeSelectorItem) && GUILayout.Button("Change Type"))
            {
                var temp = new GameObject("Temp");
                var holder = temp.AddComponent<AavTypeSelectorItem>();
                AavMenuItem.CopyBasicData(entry, holder);

                var go = entry.gameObject;
                DestroyImmediate(entry);
                var newComponent = go.AddComponent<AavTypeSelectorItem>();

                AavTypeSelectorItem.CopyBasicData(holder, newComponent);
                DestroyImmediate(temp);

                Data.Dirty = true;
                AddTypeSelectedHandlers();
                EditorApplication.delayCall += GenerateMenu;
                return;
            }
            GUI.backgroundColor = resetCol;
            GUILayout.Space(16);

            // Refresh menu if necessary
            if (modified)
            {
                Data.Dirty = true;
                EditorApplication.delayCall += GenerateMenu;
            }
        }

        private void DrawSyncButton()
        {
            var dataSer = new SerializedObject(Data);

            // Avatar selector
            dataSer.Update();
            var avatarProp = dataSer.FindProperty("Avatar");
            EditorGUILayout.PropertyField(avatarProp);
            var menuProp = dataSer.FindProperty("Menu");
            EditorGUILayout.PropertyField(menuProp);

            GUILayout.Space(4);
            if (importFoldoutOpen = EditorGUILayout.Foldout(importFoldoutOpen, "Import Existing Menu"))
            {
                GUILayout.Space(4);
                Data.ImportFromMenu = EditorGUILayout.ObjectField("Import From", Data.ImportFromMenu, typeof(VRCExpressionsMenu), false) as VRCExpressionsMenu;
                if (Data.ImportFromMenu != null && GUILayout.Button("Import!"))
                {
                    if (EditorUtility.DisplayDialog("Import Menu", "Are you sure you want to import this menu into the hierarchy? This will overwrite any existing menu entries on this AAV instance.", "Yes", "No"))
                    {
                        while (Data.Root.transform.childCount > 0)
                        {
                            DestroyImmediate(Data.Root.transform.GetChild(0).gameObject);
                        }
                        CurrentMenu = Data.Root;
                        Selection.SetActiveObjectWithContext(Data, null);
                        ImportMenuRecursive(Data.ImportFromMenu, Data.Root);
                        Data.ImportFromMenu = null;
                        Data.Dirty = true;
                        importFoldoutOpen = false;
                        EditorApplication.delayCall += GenerateMenu;
                    }
                }
            }
            GUILayout.Space(4);
            if (expertFoldoutOpen = EditorGUILayout.Foldout(expertFoldoutOpen, "Expert Settings"))
            {
                var writeDefaultsProp = dataSer.FindProperty("WriteDefaults");
                EditorGUILayout.PropertyField(writeDefaultsProp);
            }
            GUILayout.Space(4);

            if (dataSer.hasModifiedProperties)
            {
                Data.Dirty = true;
            }
            dataSer.ApplyModifiedProperties();

            var error = HandleAvatarErrors();

            // Big blue sync button
            if (!error)
            {
                GUILayout.Space(16);
                var resetCol = GUI.backgroundColor;
                if (Data.Dirty)
                {
                    GUI.backgroundColor = new Color(0.4f, 0.4f, 1.0f);
                }
                if (GUILayout.Button("Synchronize!", AavHelpers.BigButtonStyle))
                {
                    Data.Dirty = false;
                    AavGenerator.Generate(Data);
                }
                GUI.backgroundColor = resetCol;
            }

            GUILayout.Space(16);
            GmgLayoutHelper.Divisor(1);
            GUILayout.Space(10);
        }

        private void EnsureGeneratedFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/" + GeneratedFolder))
            {
                AssetDatabase.CreateFolder("Assets", GeneratedFolder);
            }
        }

        private bool HandleAvatarErrors()
        {
            // Avatar errors
            var error = false;
            if (Data.Avatar == null)
            {
                error = true;
                EditorGUILayout.HelpBox("No avatar selected!", MessageType.Error);

                var avatarsInScene = GameObject.FindObjectsOfType<VRCAvatarDescriptor>();
                if (avatarsInScene.Length == 1 && GUILayout.Button($"Auto Fix (Use '{avatarsInScene[0].gameObject.name}')"))
                {
                    Data.Avatar = avatarsInScene[0];
                    Data.Dirty = true;
                }
            }
            else
            {
                if (!Data.Avatar.customizeAnimationLayers)
                {
                    error = true;
                    EditorGUILayout.HelpBox("Custom Layers are disabled! Please press 'Customize' on 'Playable Layers' on your avatar descriptor.", MessageType.Error);
                    if (GUILayout.Button("Auto Fix"))
                    {
                        Data.Avatar.customizeAnimationLayers = true;
                        EditorUtility.SetDirty(Data.Avatar);
                        Data.Dirty = true;
                    }
                }
                else if (Data.Avatar.baseAnimationLayers[4].isDefault || Data.Avatar.baseAnimationLayers[4].animatorController == null)
                {
                    error = true;
                    EditorGUILayout.HelpBox("No FX controller available! Please create a new 'Animator Controller' and assign it as 'FX' on your avatar descriptor.", MessageType.Error);
                    if (GUILayout.Button("Auto Fix"))
                    {
                        EnsureGeneratedFolderExists();
                        var fx = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(
                            AssetDatabase.GenerateUniqueAssetPath($"Assets/{GeneratedFolder}/FX-{Data.Avatar.gameObject.name}.controller")
                        );
                        Data.Avatar.baseAnimationLayers[4].animatorController = fx;
                        Data.Avatar.baseAnimationLayers[4].isDefault = false;
                        Data.Avatar.baseAnimationLayers[4].isEnabled = true;
                        EditorUtility.SetDirty(Data.Avatar);
                        Data.Dirty = true;
                    }
                }

                if (!Data.Avatar.customExpressions)
                {
                    error = true;
                    EditorGUILayout.HelpBox("Custom Expressions are disabled! Please press 'Customize' on 'Expressions' on your avatar descriptor.", MessageType.Error);
                    if (GUILayout.Button("Auto Fix"))
                    {
                        Data.Avatar.customExpressions = true;
                        EditorUtility.SetDirty(Data.Avatar);
                        Data.Dirty = true;
                    }
                }
                else
                {
                    if (Data.Avatar.expressionsMenu == null)
                    {
                        error = true;
                        EditorGUILayout.HelpBox("No Expressions Menu assigned! Please create a new 'Expressions Menu' and assign it to your avatar descriptor.", MessageType.Error);
                        if (GUILayout.Button("Auto Fix"))
                        {
                            EnsureGeneratedFolderExists();
                            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                            AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath($"Assets/{GeneratedFolder}/ExpressionsMenu-{Data.Avatar.gameObject.name}.asset"));
                            Data.Avatar.expressionsMenu = menu;
                            EditorUtility.SetDirty(Data.Avatar);
                            Data.Dirty = true;
                        }
                    }
                    if (Data.Avatar.expressionParameters == null)
                    {
                        error = true;
                        EditorGUILayout.HelpBox("No Expression Parameters assigned! Please create a new 'Expression Parameter' object and assign it to your avatar descriptor.", MessageType.Error);
                        if (GUILayout.Button("Auto Fix"))
                        {
                            EnsureGeneratedFolderExists();
                            var @params = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                            AssetDatabase.CreateAsset(@params, AssetDatabase.GenerateUniqueAssetPath($"Assets/{GeneratedFolder}/ExpressionParameters-{Data.Avatar.gameObject.name}.asset"));
                            Data.Avatar.expressionParameters = @params;
                            EditorUtility.SetDirty(Data.Avatar);
                            Data.Dirty = true;
                        }
                    }
                }
            }

            return error;
        }

        private void ImportMenuRecursive(VRCExpressionsMenu menu, AavSubmenuItem parent)
        {
            foreach (var item in menu.controls)
            {
                var newObject = new GameObject(item.name);
                newObject.transform.parent = parent.transform;
                if (item.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    var submenu = newObject.AddComponent<AavSubmenuItem>();
                    submenu.Icon = item.icon;
                    ImportMenuRecursive(item.subMenu, submenu);
                }
                else
                {
                    var raw = newObject.AddComponent<AavRawMenuItem>();
                    raw.SetDataFromControl(item);
                }
            }
        }
    }
}
#endif