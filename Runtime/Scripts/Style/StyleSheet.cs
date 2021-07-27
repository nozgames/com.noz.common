using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Style
{
    public class StyleSheet : ScriptableObject
    {
        private static Regex ParseRegex = new Regex(
            @"([A-Za-z][\w_-]*|\#[A-Za-z][\w_\-\:]*|{|}|;|:|\.|\d\.?\d*|\#[\dA-Fa-f]+|//.*\n)", 
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        public static event Action onReload;

        [Serializable]
        private class SerializedProperty
        {
            public string name;
            public string value;
        }

        [Serializable]
        private struct SerializedSelector
        {
            public string name;
            public Style.State state;
        }

        [Serializable]
        private class SerializedStyle
        {
            public SerializedSelector selector;
            public SerializedSelector inherit;
            public SerializedProperty[] properties;
        }

        [SerializeField] private SerializedStyle[] _styles;
        [SerializeField] private string _error;
        [SerializeField] private int _errorLine;

        private Dictionary<int, Dictionary<ulong, StylePropertyValue>> _properties;

        private Dictionary<ulong, ulong> _selectorBase = new Dictionary<ulong, ulong>();

        public bool hasError => !string.IsNullOrEmpty(_error);
        public string error => _error;
        public int errorLine => _errorLine;

        public static void ReloadAll ()
        {
            onReload?.Invoke();
        }

        private static ulong MakeSelector (int hash, Style.State state) => ((ulong)hash) + (((ulong)state) << 32);

        private StylePropertyValue Search (Dictionary<ulong, StylePropertyValue> properties, ulong selector)
        {
            if (properties.TryGetValue(selector, out var property))
                return property;

            // Check base if there is one
            if(_selectorBase.TryGetValue(selector, out var baseSelector))
            {
                var baseProperty = Search(properties, baseSelector);
                if (null != baseProperty)
                    return baseProperty;
            }

            // Create null property
            properties.Add(selector, null);

            return null;
        }

        private StylePropertyValue Search(Style style, int propertyId)
        {
            if (null == _properties)
                BuildPropertyDictionary();

            if (!_properties.TryGetValue(propertyId, out var properties))
                return null;

            var propertyValue = Search(properties, MakeSelector(style.idHash, style.state));
            if (propertyValue != null)
                return propertyValue;

            var state = style.state;
            if (state == Style.State.SelectedHover)
                propertyValue = Search(properties, MakeSelector(style.idHash, Style.State.Hover));

            if (null == propertyValue && state == Style.State.SelectedPressed)
                propertyValue = Search(properties, MakeSelector(style.idHash, Style.State.Pressed));

            if (null == propertyValue && state != Style.State.Normal)
                propertyValue = Search(properties, MakeSelector(style.idHash, Style.State.Normal));

            return propertyValue;
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

        private class Token
        {
            public string value;
            public int line;
            public int index;
        }

        private class ParseException : Exception
        {
            public int line;
            public ParseException(Token token, string message) : base(message)
            {
                line = token.line;
            }
        }

        public static StyleSheet Parse(string text)
        {
            var matches = ParseRegex.Matches(text);
            if (matches.Count == 0)
                return null;

            var tokens = new List<Token>(matches.Count);
            var line = 1;
            var previousIndex = -1;
            for(int i=0; i< matches.Count; i++)
            {
                var match = matches[i];
                for (var index = match.Index; index > previousIndex; index--)
                    if (text[index] == '\n')
                        line++;

                previousIndex = match.Index;

                var value = match.Value.Trim();
                if (value == "" || value.StartsWith("//"))
                    continue;

                tokens.Add(new Token { index = matches[i].Index, value = value, line = line });
            }

            var sheet = CreateInstance<StyleSheet>();

            try
            {
                // Parse all of the styles
                var styles = new List<SerializedStyle>();
                for (var tokenIndex = 0; tokenIndex < tokens.Count;)
                    sheet.ParseStyle(tokens, ref tokenIndex, styles);

                sheet._styles = styles.ToArray();
            } 
            catch (IndexOutOfRangeException)
            {
                sheet._errorLine = tokens[tokens.Count - 1].line;
                sheet._error = $"unexpected EOF";
            } 
            catch (ParseException e)
            {
                sheet._errorLine = e.line;
                sheet._error = $"{e.Message}";
            }

            return sheet;
        }

        private ulong ParseSelector (List<Token> tokens, ref int tokenIndex, ref SerializedSelector serializedSelector)
        {
            var token = tokens[tokenIndex++].value;
            if(token[0] != '#')
                throw new ParseException(tokens[tokenIndex-1], "selector must begin with #");

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
                    state = Style.State.Disabled;
                else if (stateName == "pressed")
                    state = Style.State.Pressed;
                else if (stateName == "selected")
                    state = Style.State.Selected;
                else if (stateName == "selected:hover")
                    state = Style.State.SelectedHover;
                else if (stateName == "selected:pressed")
                    state = Style.State.SelectedPressed;
                else
                    throw new ParseException(tokens[tokenIndex - 1], $"unknown state \"{stateName}\"");
            }

            serializedSelector.name = name;
            serializedSelector.state = state;

            return MakeSelector(Style.StringToHash(name), state);
        }

        private void ParseStyle (List<Token> tokens, ref int tokenIndex, List<SerializedStyle> serializedStyles)
        {
            var serializedStyle = new SerializedStyle();
            ParseSelector(tokens, ref tokenIndex, ref serializedStyle.selector);

            // Base style?
            if(tokens[tokenIndex].value == ":")
            {
                tokenIndex++;
                ParseSelector(tokens, ref tokenIndex, ref serializedStyle.inherit);

                var found = false;
                foreach(var style in serializedStyles)
                {
                    if(style.selector.name == serializedStyle.inherit.name && style.selector.state == serializedStyle.inherit.state)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new ParseException(tokens[tokenIndex - 1], $"unknown base selector \"{tokens[tokenIndex - 1].value}\"");
            }

            if (tokens[tokenIndex++].value != "{")
                throw new ParseException(tokens[tokenIndex - 1], "missing \"{\"");

            serializedStyles.Add(serializedStyle);

            // Read until the end brace
            var serializedProperties = new List<SerializedProperty>();
            while (tokens[tokenIndex].value != "}")
                ParseProperty(tokens, ref tokenIndex, serializedProperties);

            serializedStyle.properties = serializedProperties.ToArray();

            // SKip end brace
            tokenIndex++;
        }

        private void ParseProperty(List<Token> tokens, ref int tokenIndex, List<SerializedProperty> serializedProperties)
        {
            var name = tokens[tokenIndex++].value;
            if (tokens[tokenIndex++].value != ":")
                throw new ParseException(tokens[tokenIndex - 1], "Missing \":\"");

            var value = tokens[tokenIndex++].value;
            if (tokens[tokenIndex++].value != ";")
                throw new ParseException(tokens[tokenIndex - 1], "Missing \";\"");

            serializedProperties.Add(new SerializedProperty { name = name, value = value });
        }

        private void SetBaseSelector (ulong selector, ulong baseSelector)
        {
            if (baseSelector == selector)
                return;

            var checkBaseSelector = baseSelector;
            while(_selectorBase.TryGetValue(checkBaseSelector, out checkBaseSelector))
            {
                if (checkBaseSelector == selector)
                    return;
            }

            _selectorBase[selector] = baseSelector;
        }

        private void BuildPropertyDictionary()
        {
            // Populate the dictionary from the serialized values
            _properties = new Dictionary<int, Dictionary<ulong, StylePropertyValue>>();
            _selectorBase = new Dictionary<ulong, ulong>();

            if (_styles == null)
                return;

            foreach (var serializedStyle in _styles)
            {
                var styleNameHashId = Style.StringToHash(serializedStyle.selector.name);
                var selector = MakeSelector(styleNameHashId, serializedStyle.selector.state);

                // Inheritence
                if(!string.IsNullOrEmpty(serializedStyle.inherit.name))
                {
                    var inheritNameHashId = Style.StringToHash(serializedStyle.inherit.name);
                    // If a state is specified then inherit only that state
                    if (serializedStyle.selector.state != Style.State.Normal)
                        SetBaseSelector(selector,MakeSelector(inheritNameHashId, serializedStyle.inherit.state));
                    // Set all states to one state?
                    else if (serializedStyle.inherit.state != Style.State.Normal)
                    {
                        var baseSelector = MakeSelector(inheritNameHashId, serializedStyle.inherit.state);
                        foreach (var state in Enum.GetValues(typeof(Style.State)) as Style.State[])
                            SetBaseSelector(MakeSelector(styleNameHashId, state), baseSelector);
                    }
                    else
                    {
                        foreach(var state in Enum.GetValues(typeof(Style.State)) as Style.State[])
                            SetBaseSelector(MakeSelector(styleNameHashId, state), MakeSelector(inheritNameHashId, state));
                    }
                }
                    

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

