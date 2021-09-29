using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NoZ.Stylez
{
    [CustomEditor(typeof(StylezSheet))]
    public class StyleSheetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sheet = target as StylezSheet;

            GUI.enabled = true;

            if(sheet.hasError)
                EditorGUILayout.HelpBox(sheet.error, MessageType.Error);
        }
    }
}
