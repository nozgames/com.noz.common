using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NoZ.Style
{
    [CustomEditor(typeof(StyleSheet))]
    public class StyleSheetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sheet = target as StyleSheet;

            GUI.enabled = true;

            if(sheet.hasError)
                EditorGUILayout.HelpBox(sheet.error, MessageType.Error);
        }
    }
}
