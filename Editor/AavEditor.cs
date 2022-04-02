#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

using GestureManager.Scripts.Core.Editor;
using GestureManager.Scripts.Editor.Modules.Vrc3;
using GestureManager.Scripts.Editor.Modules.Vrc3.RadialButtons;

using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

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
        private const int CursorSize = 50;

        private AnimatorAsVisual Data => target as AnimatorAsVisual;
        public override bool RequiresConstantRepaint() => true; // FIXME? Performance?

        private bool modified = false;
        private bool expertFoldoutOpen = false;

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
                            Data.Root = ScriptableObject.CreateInstance<AavSubmenuItem>();
                            Data.Root.AavName = "Root";
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

        /*
            VisualElements containing the GestureManager dial UI -
            this and accompanying code is very much taken and adapted from GestureManager itself.
        */
        private VisualElement root;

        private RadialCursor cursor;
        private VisualElement borderHolder;
        private VisualElement dataHolder;
        private VisualElement puppetHolder;
        private VisualElement radial;

        private RadialMenuItem[] buttons;

        private DateTime lastClick = DateTime.MinValue;

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
            radial = RadialMenuUtility.Prefabs.NewCircle(Size, RadialMenuUtility.Colors.RadialCenter, RadialMenuUtility.Colors.RadialMiddle, RadialMenuUtility.Colors.RadialBorder);
            // overlap with imgui rectangle to get correct input events
            radial.style.top = -Size;
            radial.style.marginBottom = -Size;
            radial.style.alignSelf = Align.Center;

            borderHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });
            radial.MyAdd(RadialMenuUtility.Prefabs.NewCircle((int)InnerSize, RadialMenuUtility.Colors.RadialInner, RadialMenuUtility.Colors.OuterBorder, Position.Absolute));

            dataHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });
            puppetHolder = radial.MyAdd(new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute } });

            cursor = new RadialCursor(CursorSize);
            radial.MyAdd(cursor);
            cursor.SetData(Clamp, ClampReset, (int)(InnerSize / 2f), (int)(Size / 2f), radial);

            LoadResources();
            EditorApplication.delayCall += GenerateMenu;
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
            cursor.Update(pos);
        }

        private void OnClickStart(Vector2 pos)
        {
            var choice = cursor.GetChoice(pos, borderHolder);
            if (choice != -1 && buttons != null) buttons[choice].OnClickStart();
        }

        private void OnClickEnd(Vector2 pos)
        {
            var choice = cursor.GetChoice(pos, borderHolder);
            if (choice != -1 && buttons != null) buttons[choice].OnClickEnd();
        }

        private void SetButtons(RadialMenuItem[] buttons)
        {
            this.buttons = buttons;

            borderHolder.Clear();
            dataHolder.Clear();

            var step = 360f / this.buttons.Length;
            var current = step / 2 - 90;

            var rStep = Mathf.PI * 2 / this.buttons.Length;
            var rCurrent = Mathf.PI;

            foreach (var item in this.buttons)
            {
                item.Create(Size);
                borderHolder.MyAdd(item.Border).transform.rotation = Quaternion.Euler(0, 0, current);

                item.DataHolder.transform.position = new Vector3(Mathf.Sin(rCurrent) * Size / 3, Mathf.Cos(rCurrent) * Size / 3, 0);

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
                current += step;
                rCurrent -= rStep;
            }
        }

        /*
            Visual editor functionality
        */

        private void GenerateMenu()
        {
            if (Data.CurrentlySelected >= CurrentMenu.Items.Count) Data.CurrentlySelected = -1;

            var list = new List<RadialMenuItem>();
            list.Add(new RadialMenuButton(HandleAddControl, "Add Control", iconPlus));

            for (int i = 0; i < CurrentMenu.Items.Count; i++)
            {
                var item = CurrentMenu.Items[i];
                
                AavSubmenuItem sub;
                if ((sub = item as AavSubmenuItem) != null)
                {
                    sub.MenuOpened = menu =>
                    {
                        OpenSubmenu((AavSubmenuItem)menu);
                    };
                }

                list.Add(new RadialMenuButton(HandleEntrySelected(i), item.AavName, item.Icon));
            }

            SetButtons(list.ToArray());
        }

        // curry the index into an Action
        // mmmh curry
        private Action HandleEntrySelected(int index)
        {
            return () =>
            {
                var entry = CurrentMenu.Items[index];
                AavSubmenuItem sub;
                if ((sub = entry as AavSubmenuItem) != null)
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastClick).TotalMilliseconds < 250)
                    {
                        // double click on submenu, open it
                        lastClick = DateTime.MinValue;
                        OpenSubmenu(sub);
                    }
                    else
                    {
                        lastClick = now;
                    }
                }

                if (Data.CurrentlySelected == index)
                    Data.CurrentlySelected = -1;
                else
                    Data.CurrentlySelected = index;
                GenerateMenu();
            };
        }

        private void HandleAddControl()
        {
            var selectorItem = ScriptableObject.CreateInstance<AavTypeSelectorItem>();
            selectorItem.AavName = "New Entry";
            selectorItem.TypeSelected += HandleControlTypeSelected;
            CurrentMenu.Items.Add(selectorItem);

            Data.CurrentlySelected = CurrentMenu.Items.Count - 1;
            Data.Dirty = true;

            GenerateMenu();
        }

        private void HandleControlTypeSelected(object sender, Type type)
        {
            var sel = (AavTypeSelectorItem)sender;

            var newEntry = (AavMenuItem)ScriptableObject.CreateInstance(type);
            newEntry.AavName = sel.AavName;
            newEntry.Icon = sel.Icon;

            CurrentMenu.Items[Data.CurrentlySelected] = newEntry;

            AavSubmenuItem sub;
            if ((sub = newEntry as AavSubmenuItem) != null)
            {
                sub.Parent = CurrentMenu;
            }

            ScriptableObject.DestroyImmediate(sel);
            GenerateMenu();
        }

        private void OpenSubmenu(AavSubmenuItem menu)
        {
            CurrentMenu = menu;
            Data.CurrentlySelected = -1;
            EditorApplication.delayCall += GenerateMenu;
        }

        /*
            Editor drawing logic
        */
        private void DrawEntryEditor()
        {
            GUILayout.Space(14);

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
            if (CurrentMenu.Parent != null && GUILayout.Button("^ Up ^"))
            {
                var prev = CurrentMenu;
                CurrentMenu = CurrentMenu.Parent;
                Data.CurrentlySelected = -1;
                // select menu that was just open
                for (int i = 0; i < CurrentMenu.Items.Count; i++)
                {
                    if (CurrentMenu.Items[i] == prev)
                    {
                        Data.CurrentlySelected = i;
                        break;
                    }
                }
                EditorApplication.delayCall += GenerateMenu;
                return;
            }

            GUILayout.Space(10);
            GmgLayoutHelper.Divisor(1);
            GUILayout.Space(10);

            if (Data.CurrentlySelected >= CurrentMenu.Items.Count) Data.CurrentlySelected = -1;
            if (Data.CurrentlySelected == -1)
            {
                GUILayout.Label("Please select an entry to edit.");
                GUILayout.Space(10);
                return;
            }

            modified = false;
            var entry = CurrentMenu.Items[Data.CurrentlySelected];

            Undo.RecordObject(entry, "Animator As Visual");

            // Draw and handle move buttons
            if (CurrentMenu.Items.Count >= 2)
            {
                using (var horoscope /* I'm an Aquarius */ = new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("<< CCW"))
                    {
                        if (Data.CurrentlySelected == 0)
                        {
                            var toAppend = CurrentMenu.Items[0];
                            for (int i = 1; i < CurrentMenu.Items.Count; i++)
                            {
                                CurrentMenu.Items[i - 1] = CurrentMenu.Items[i];
                            }
                            CurrentMenu.Items[CurrentMenu.Items.Count - 1] = toAppend;
                            Data.CurrentlySelected = CurrentMenu.Items.Count - 1;
                        }
                        else
                        {
                            var toSwap = CurrentMenu.Items[Data.CurrentlySelected];
                            CurrentMenu.Items[Data.CurrentlySelected] = CurrentMenu.Items[Data.CurrentlySelected - 1];
                            CurrentMenu.Items[Data.CurrentlySelected - 1] = toSwap;
                            Data.CurrentlySelected--;
                        }

                        modified = true;
                    }
                    GUILayout.Space(5);
                    if (GUILayout.Button("CW >>"))
                    {
                        if (Data.CurrentlySelected == CurrentMenu.Items.Count - 1)
                        {
                            var toPrepend = CurrentMenu.Items[CurrentMenu.Items.Count - 1];
                            for (int i = CurrentMenu.Items.Count - 1; i >= 1; i--)
                            {
                                CurrentMenu.Items[i] = CurrentMenu.Items[i - 1];
                            }
                            CurrentMenu.Items[0] = toPrepend;
                            Data.CurrentlySelected = 0;
                        }
                        else
                        {
                            var toSwap = CurrentMenu.Items[Data.CurrentlySelected];
                            CurrentMenu.Items[Data.CurrentlySelected] = CurrentMenu.Items[Data.CurrentlySelected + 1];
                            CurrentMenu.Items[Data.CurrentlySelected + 1] = toSwap;
                            Data.CurrentlySelected++;
                        }

                        modified = true;
                    }
                }
            }

            // Draw basic editor fields
            var headerStyle = AavHelpers.HeaderStyle;
            GUILayout.Label(entry.GUIName, headerStyle);

            entry.AavName = entry.AavName.UpdateWith(() => EditorGUILayout.TextField("Entry Name", entry.AavName), ref modified);
            GUILayout.Space(4);
            entry.Icon = entry.Icon.UpdateWith(() => (Texture2D)EditorGUILayout.ObjectField("Menu Icon", entry.Icon, typeof(Texture2D), false), ref modified);
            GUILayout.Space(4);

            // Draw the editor for the currently selected entry now
            modified |= entry.DrawEditor();

            // Big red delete button
            GUILayout.Space(16);
            var resetCol = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Entry", AavHelpers.BigButtonStyle))
            {
                CurrentMenu.Items.RemoveAt(Data.CurrentlySelected);
                ScriptableObject.DestroyImmediate(entry);
                Data.CurrentlySelected--;
                Data.Dirty = true;
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
            // Avatar selector
            serializedObject.Update();
            var avatarProp = serializedObject.FindProperty("Avatar");
            EditorGUILayout.PropertyField(avatarProp);

            GUILayout.Space(4);
            if (expertFoldoutOpen = EditorGUILayout.Foldout(expertFoldoutOpen, "Expert Settings"))
            {
                var writeDefaultsProp = serializedObject.FindProperty("WriteDefaults");
                EditorGUILayout.PropertyField(writeDefaultsProp);
            }
            GUILayout.Space(4);

            if (serializedObject.hasModifiedProperties)
            {
                Data.Dirty = true;
            }
            serializedObject.ApplyModifiedProperties();

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
    }
}
#endif