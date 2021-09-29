using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NoZ.StylezEditor
{
    public class StylezSelectableEditor : Editor
    {
        private SerializedProperty m_Script;

        private SerializedProperty m_InteractableProperty;

        private SerializedProperty m_NavigationProperty;

        private GUIContent m_VisualizeNavigation = EditorGUIUtility.TrTextContent("Visualize", "Show navigation flows between selectable UI elements.");

        private static List<StylezSelectableEditor> s_Editors = new List<StylezSelectableEditor>();

        private static bool s_ShowNavigation = false;

        private static string s_ShowNavigationKey = "SelectableEditor.ShowNavigation";

        private string[] m_PropertyPathToExcludeForChildClasses;

        protected virtual void OnEnable()
        {
            m_Script = base.serializedObject.FindProperty("m_Script");
            m_InteractableProperty = base.serializedObject.FindProperty("m_Interactable");
            m_NavigationProperty = base.serializedObject.FindProperty("m_Navigation");
            m_PropertyPathToExcludeForChildClasses = new string[]
            {
                m_Script.propertyPath,
                m_NavigationProperty.propertyPath,
                m_InteractableProperty.propertyPath
            };
            s_Editors.Add(this);
            RegisterStaticOnSceneGUI();
            s_ShowNavigation = EditorPrefs.GetBool(s_ShowNavigationKey);
        }

        protected virtual void OnDisable()
        {
            s_Editors.Remove(this);
            RegisterStaticOnSceneGUI();
        }

        private void RegisterStaticOnSceneGUI()
        {
            SceneView.duringSceneGui -= StaticOnSceneGUI;
            if (s_Editors.Count > 0)
            {
                SceneView.duringSceneGui += StaticOnSceneGUI;
            }
        }

        public override void OnInspectorGUI()
        {
            base.serializedObject.Update();
            EditorGUILayout.PropertyField(m_InteractableProperty);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_NavigationProperty);
            EditorGUI.BeginChangeCheck();
            Rect controlRect2 = EditorGUILayout.GetControlRect();
            controlRect2.xMin += EditorGUIUtility.labelWidth;
            s_ShowNavigation = GUI.Toggle(controlRect2, s_ShowNavigation, m_VisualizeNavigation, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(s_ShowNavigationKey, s_ShowNavigation);
                SceneView.RepaintAll();
            }

            ChildClassPropertiesGUI();
            base.serializedObject.ApplyModifiedProperties();
        }

        private void ChildClassPropertiesGUI()
        {
            if (!IsDerivedSelectableEditor())
            {
                Editor.DrawPropertiesExcluding(base.serializedObject, m_PropertyPathToExcludeForChildClasses);
            }
        }

        private bool IsDerivedSelectableEditor()
        {
            return GetType() != typeof(StylezSelectableEditor);
        }

        private static void StaticOnSceneGUI(SceneView view)
        {
            if (!s_ShowNavigation)
            {
                return;
            }

            Selectable[] allSelectablesArray = Selectable.allSelectablesArray;
            foreach (Selectable selectable in allSelectablesArray)
            {
                if (StageUtility.IsGameObjectRenderedByCamera(selectable.gameObject, Camera.current))
                {
                    DrawNavigationForSelectable(selectable);
                }
            }
        }

        private static void DrawNavigationForSelectable(Selectable sel)
        {
            if (!(sel == null))
            {
                Transform transform = sel.transform;
                bool flag = Selection.transforms.Any((Transform e) => e == transform);
                Handles.color = new Color(1f, 0.6f, 0.2f, flag ? 1f : 0.4f);
                DrawNavigationArrow(-Vector2.right, sel, sel.FindSelectableOnLeft());
                DrawNavigationArrow(Vector2.up, sel, sel.FindSelectableOnUp());
                Handles.color = new Color(1f, 0.9f, 0.1f, flag ? 1f : 0.4f);
                DrawNavigationArrow(Vector2.right, sel, sel.FindSelectableOnRight());
                DrawNavigationArrow(-Vector2.up, sel, sel.FindSelectableOnDown());
            }
        }

        private static void DrawNavigationArrow(Vector2 direction, Selectable fromObj, Selectable toObj)
        {
            if (!(fromObj == null) && !(toObj == null))
            {
                Transform transform = fromObj.transform;
                Transform transform2 = toObj.transform;
                Vector2 vector = new Vector2(direction.y, 0f - direction.x);
                Vector3 vector2 = transform.TransformPoint(GetPointOnRectEdge(transform as RectTransform, direction));
                Vector3 vector3 = transform2.TransformPoint(GetPointOnRectEdge(transform2 as RectTransform, -direction));
                float d = HandleUtility.GetHandleSize(vector2) * 0.05f;
                float d2 = HandleUtility.GetHandleSize(vector3) * 0.05f;
                vector2 += transform.TransformDirection(vector) * d;
                vector3 += transform2.TransformDirection(vector) * d2;
                float d3 = Vector3.Distance(vector2, vector3);
                Vector3 b = transform.rotation * direction * d3 * 0.3f;
                Vector3 b2 = transform2.rotation * -direction * d3 * 0.3f;
                Handles.DrawBezier(vector2, vector3, vector2 + b, vector3 + b2, Handles.color, null, 2.5f);
                Handles.DrawAAPolyLine(2.5f, vector3, vector3 + transform2.rotation * (-direction - vector) * d2 * 1.2f);
                Handles.DrawAAPolyLine(2.5f, vector3, vector3 + transform2.rotation * (-direction + vector) * d2 * 1.2f);
            }
        }

        private static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        {
            if (rect == null)
            {
                return Vector3.zero;
            }

            if (dir != Vector2.zero)
            {
                dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            }

            dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
            return dir;
        }
    }
}
