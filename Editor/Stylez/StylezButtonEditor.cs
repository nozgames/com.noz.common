using UnityEditor;
using NoZ.Stylez;

namespace NoZ.StylezEditor
{
    [CustomEditor(typeof(StylezButton), true)]
    public class StylezButtonEditor : StylezSelectableEditor
    {
        private SerializedProperty m_OnClick;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_OnClick = serializedObject.FindProperty("m_OnClick");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_OnClick);
        }
    }
}

