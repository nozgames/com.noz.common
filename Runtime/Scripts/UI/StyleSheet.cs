using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.UI
{
    public class StyleSheet : ScriptableObject
    {
        private static Regex ParseRegex = new Regex(
            @"([A-Za-z][\w_-]*|\#[A-Za-z][\w_\-\:]*|{|}|;|:|\.|\d\.?\d*|\#[\dA-Fa-f]+)", 
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        public static event Action onReload;

        [Serializable]
        private class SerializedProperty
        {
            public string name;
            public string value;
        }

        [Serializable]
        private class SerializedStyle
        {
            public string name;
            public Style.State state;
            public SerializedProperty[] properties;
        }

        [SerializeField] private SerializedStyle[] _styles;

        private Dictionary<int, Dictionary<ulong, StylePropertyValue>> _properties;


        public static void ReloadAll ()
        {
            onReload?.Invoke();
        }

        private static ulong MakeSelector (int hash, Style.State state) => ((ulong)hash) + (((ulong)state) << 32);

        private StylePropertyValue Search (Dictionary<ulong, StylePropertyValue> properties, int hash, int baseHash, Style.State state)
        {
            var key = MakeSelector(hash, state);
            if (properties.TryGetValue(key, out var property))
                return property;

            // Check base if there is one
            if (baseHash != 0)
            {
                var baseProperty = Search(properties, baseHash, 0, state);
                if (null != baseProperty)
                    return baseProperty;
            }

            if (state == Style.State.SelectedHover || state == Style.State.SelectedPressed)
                return Search(properties, hash, baseHash, Style.State.Selected);

            // If the state wasnt normal check normal too if we cant find a state
            if (state != Style.State.Normal)
                return Search(properties, hash, baseHash, Style.State.Normal);

            // Create null property
            properties.Add(key, null);

            return null;
        }

        private StylePropertyValue Search(Style style, int propertyId)
        {
            if (null == _properties)
                BuildPropertyDictionary();

            if (!_properties.TryGetValue(propertyId, out var properties))
                return null;

            return Search(properties, style.idHash, style.inheritHash, style.state);
        }

        public T GetValue<T> (Style style, string propertyName, T defaultValue) => 
            GetValue<T>(style, Style.StringToHash(propertyName), defaultValue);

        public T GetValue<T> (Style style, int propertyNameHashId, T defaultValue)
        {
            var property = Search(style, propertyNameHashId) as StylePropertyValue<T>;
            if (null == property)
            {
                if (style.parent != null)
                    return GetValue<T>(style.parent, propertyNameHashId, defaultValue);

                return defaultValue;
            }

            return property.value;
        }


        public static StyleSheet Parse(string text)
        {
            var tokens = ParseRegex.Matches(text);
            if (tokens.Count == 0)
                return null;

            try
            {
                var sheet = CreateInstance<StyleSheet>();

                // Parse all of the styles
                var styles = new List<SerializedStyle>();
                for (var tokenIndex = 0; tokenIndex < tokens.Count;)
                    sheet.ParseStyle(text, tokens, ref tokenIndex, styles);

                sheet._styles = styles.ToArray();

                return sheet;

            } 
            catch (IndexOutOfRangeException)
            {
                Debug.LogError($"error: {GetLineNumber(text, text.Length - 1)}: unexpected EOF ");
            } 
            catch (Exception e)
            {
                Debug.LogError($"error: {e.Message} ");
            }

            return null;
        }

        private ulong ParseSelector (string text, MatchCollection tokens, ref int tokenIndex, SerializedStyle serializedStyle)
        {
            var token = tokens[tokenIndex++].Value;
            if(token.Length < 0 || token[0] != '#')
                throw new FormatException($"{GetLineNumber(text, tokens[tokenIndex-1])}: missing selector");

            var name = token.Substring(1);
            var state = Style.State.Normal;

            var colon = name.IndexOf(':');
            if (colon != -1)
            { 
                var stateName = name.Substring(colon + 1).ToLower();
                name = name.Substring(0, colon);

                if (stateName == "hover")
                    state = Style.State.Hover;
                else if (stateName == "disabled")
                    state = Style.State.Hover;
                else if (stateName == "pressed")
                    state = Style.State.Hover;
                else if (stateName == "selected")
                    state = Style.State.Selected;
                else if (stateName == "selected:hover")
                    state = Style.State.SelectedHover;
                else if (stateName == "selected:pressed")
                    state = Style.State.SelectedPressed;
            }

            serializedStyle.name = name;
            serializedStyle.state = state;

            return MakeSelector(Style.StringToHash(name), state);
        }

        private void ParseStyle (string text, MatchCollection tokens, ref int tokenIndex, List<SerializedStyle> serializedStyles)
        {
            var serializedStyle = new SerializedStyle();
            var selector = ParseSelector(text, tokens, ref tokenIndex, serializedStyle);

            if (tokens[tokenIndex++].Value != "{")
                throw new FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: missing \"{{\"");

            serializedStyles.Add(serializedStyle);

            // Read until the end brace
            var serializedProperties = new List<SerializedProperty>();
            while (tokens[tokenIndex].Value != "}")
                ParseProperty(selector, text, tokens, ref tokenIndex, serializedProperties);

            serializedStyle.properties = serializedProperties.ToArray();

            // SKip end brace
            tokenIndex++;
        }

        private void ParseProperty(ulong selector, string text, MatchCollection tokens, ref int tokenIndex, List<SerializedProperty> serializedProperties)
        {
            var name = tokens[tokenIndex++].Value;
            if (tokens[tokenIndex++].Value != ":")
                throw new FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: Missing \":\"");

            var value = tokens[tokenIndex++].Value;

            if (tokens[tokenIndex++].Value != ";")
                throw new FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: Missing \";\"");

            serializedProperties.Add(new SerializedProperty { name = name, value = value });

            Debug.Log($"{selector:X016} {name} = {value}");
        }

        private static int GetLineNumber(string text, Match match) => GetLineNumber(text, match.Index);

        private static int GetLineNumber(string text, int index)
        {
            var lineNumber = 1;
            for (var i = index; i >= 0; i--)
                lineNumber += text[i] == '\n' ? 1 : 0;

            return lineNumber;
        }

        private void BuildPropertyDictionary()
        {
            // Populate the dictionary from the serialized values
            _properties = new Dictionary<int, Dictionary<ulong, StylePropertyValue>>();

            if (_styles == null)
                return;

            foreach (var serializedStyle in _styles)
            {
                var styleNameHashId = Style.StringToHash(serializedStyle.name);
                var selector = MakeSelector(styleNameHashId, serializedStyle.state);

                foreach (var serializedProperty in serializedStyle.properties)
                {
                    var propertyNameHashId = Style.StringToHash(serializedProperty.name);
                    if (!_properties.TryGetValue(propertyNameHashId, out var selectors))
                    {
                        selectors = new Dictionary<ulong, StylePropertyValue>();
                        _properties[propertyNameHashId] = selectors;
                    }

                    if (!Style._propertyInfos.TryGetValue(propertyNameHashId, out var propertyInfo))
                    {
                        Debug.LogWarning($"Unknown property \"{name}\" in style sheet");
                        return;
                    }

                    var property = propertyInfo.Parse(serializedProperty.value);
                    if (null == property)
                        return;

                    selectors[selector] = property;
                }
            }
        }

        private void OnEnable()
        {
            onReload += OnReload;
        }

        private void OnDisable()
        {
            onReload -= OnReload;
        }

        private void OnReload()
        {
            _properties = null;
        }
    }
}

