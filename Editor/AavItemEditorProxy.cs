#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace pi.AnimatorAsVisual
{
    [CustomEditor(typeof(AavMenuItem), true)]
    public class AavMenuItemEditor : Editor
    {
        private AavEditor aavEditor;
        private AavMenuItem item => target as AavMenuItem;

        public override bool RequiresConstantRepaint() => true; // FIXME? Performance?

        void OnEnable()
        {
            var root = item.transform;
            AnimatorAsVisual data;
            while (!root.TryGetComponent<AnimatorAsVisual>(out data))
            {
                if (root.transform.parent == null)
                {
                    throw new System.Exception("Could not find AnimatorAsVisual in hierarchy, do not place AavMenuItem outside of AnimatorAsVisual.");
                }
                root = root.transform.parent;
            }

            aavEditor = ScriptableObject.CreateInstance<AavEditor>();
            aavEditor.Data = data;
            aavEditor.OnEnable();

            UpdateSelectionWithPosition();
            EditorApplication.hierarchyChanged += UpdateSelectionWithPosition;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= UpdateSelectionWithPosition;
        }

        private void UpdateSelectionWithPosition()
        {
            AavSubmenuItem sub;
            if ((sub = item as AavSubmenuItem) != null)
            {
                aavEditor.Data.CurrentMenu = sub;
                aavEditor.Data.CurrentlySelected = -1;
            }
            else
            {
                aavEditor.Data.CurrentMenu = item.transform.GetComponentInParent<AavSubmenuItem>();
                aavEditor.Data.CurrentlySelected = item.transform.GetPositionInHierarchy();
            }
            aavEditor.GenerateMenu();
        }

        public override VisualElement CreateInspectorGUI()
        {
            return aavEditor?.CreateInspectorGUI();
        }
    }
}
#endif