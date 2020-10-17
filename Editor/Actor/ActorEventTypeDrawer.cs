using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NoZ
{
    // IngredientDrawer
    [CustomPropertyDrawer(typeof(ActorEventType))]
    public class ActorEventTypeDrawer : PropertyDrawer
    {
        private static string[] _cacheTypeName = null;
        private static string[] _cacheDisplayName = null;

        private static readonly Regex _cacheDisplayNameRegex = new Regex(@"(?:[\w\d]+\.)+([\w\d]+)(?:Event$)");

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if(null == _cacheDisplayName)
                InitCache();

            var name = property.serializedObject.FindProperty("_event").FindPropertyRelative("name");

            EditorGUI.BeginProperty(position, label, property);
            
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            EditorGUI.BeginChangeCheck();
            var typeName = name.stringValue;
            var index = 0;
            for (int i = 1; i < _cacheTypeName.Length && index == 0; i++)
                if (_cacheTypeName[i] == typeName)
                    index = i;

            index = EditorGUI.Popup(position, index, _cacheDisplayName);
            if(EditorGUI.EndChangeCheck())
            {
                if (index == 0)
                    name.stringValue = null;
                else
                    name.stringValue = _cacheTypeName[index];
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        private void InitCache()
        {
            var cache = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (var type in assembly.GetTypes().Where(t => typeof(ActorEvent).IsAssignableFrom(t) && t != typeof(ActorEvent)))
                    cache.Add(type.FullName);

            cache.Insert(0, "None");
            cache.Sort();

            _cacheTypeName = cache.ToArray();
            _cacheDisplayName = new string[_cacheTypeName.Length];
            _cacheDisplayName[0] = _cacheTypeName[0];

            for (int i = 1; i < _cacheTypeName.Length; i++)
            {
                var match = _cacheDisplayNameRegex.Match(_cacheTypeName[i]);
                _cacheDisplayName[i] = match.Success ? match.Groups[1].Value : _cacheTypeName[i];
            }
        }
    }
}
