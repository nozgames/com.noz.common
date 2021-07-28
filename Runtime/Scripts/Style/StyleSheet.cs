using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Style
{
    public class StyleSheet : ScriptableObject
    {
        private static Regex ParseRegex = new Regex(
            @"([A-Za-z][\w_-]*|\#[A-Za-z][\w_\-\:]*|{|}|;|:|,|\.|\d\.?\d*|\#[\dA-Fa-f]+|//.*\n)", 
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
            public int state;
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

        private static ulong MakeSelector(int hash, Style.State state)
        {
            if ((int)state == -1)
                state = (int)Style.State.Normal;
            return ((ulong)hash) + (((ulong)state) << 32);
        }

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

        public bool TryGetValue<T> (Style style, string propertyName, out T value) => 
            TryGetValue<T>(style, Style.StringToHash(propertyName), out value);

        public bool TryGetValue<T> (Style style, int propertyNameHashId, out T value)
        {
            var property = Search(style, propertyNameHashId) as StylePropertyValue<T>;
            if (null == property)
            {
                value = default(T);
                return false;
            }

            value = property.value;
            return true;
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

        private void ParseSelector (List<Token> tokens, ref int tokenIndex, ref SerializedSelector serializedSelector)
        {
            var token = tokens[tokenIndex++].value;
            if(token[0] != '#')
                throw new ParseException(tokens[tokenIndex-1], "selector must begin with #");

            var name = token.Substring(1);
            var state = -1;

            var colon = name.IndexOf(':');
            if (colon != -1)
            { 
                var stateName = name.Substring(colon + 1).ToLower();
                name = name.Substring(0, colon);

                if (stateName == "hover")
                    state = (int)Style.State.Hover;
                else if (stateName == "normal")
                    state = (int)Style.State.Normal;
                else if (stateName == "disabled")
                    state = (int)Style.State.Disabled;
                else if (stateName == "pressed")
                    state = (int)Style.State.Pressed;
                else if (stateName == "selected")
                    state = (int)Style.State.Selected;
                else if (stateName == "selected:hover")
                    state = (int)Style.State.SelectedHover;
                else if (stateName == "selected:pressed")
                    state = (int)Style.State.SelectedPressed;
                else
                    throw new ParseException(tokens[tokenIndex - 1], $"unknown state \"{stateName}\"");
            }

            serializedSelector.name = name;
            serializedSelector.state = state;
        }

        private void ParseStyle (List<Token> tokens, ref int tokenIndex, List<SerializedStyle> serializedStyles)
        {
            var selectors = new List<SerializedSelector>();

            while(true)
            {
                SerializedSelector selector = new SerializedSelector();
                ParseSelector(tokens, ref tokenIndex, ref selector);
                selectors.Add(selector);

                if (tokens[tokenIndex].value != ",")
                    break;

                tokenIndex++;
            } 

            // Base style?
            var baseSelector = new SerializedSelector();
            if (tokens[tokenIndex].value == ":")
            {
                tokenIndex++;
                ParseSelector(tokens, ref tokenIndex, ref baseSelector);

                var found = false;
                foreach(var style in serializedStyles)
                {
                    if(style.selector.name == baseSelector.name && style.selector.state == baseSelector.state)
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

            // Read until the end brace
            var serializedProperties = new List<SerializedProperty>();

            while (tokens[tokenIndex].value != "}")
                ParseProperty(tokens, ref tokenIndex, serializedProperties);

            // SKip end brace
            tokenIndex++;

            foreach (var selector in selectors)
            {
                var serializedStyle = new SerializedStyle();
                serializedStyle.selector = selector; ;
                serializedStyle.inherit = baseSelector;
                serializedStyle.properties = serializedProperties.ToArray();
                serializedStyles.Add(serializedStyle);
            }
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
                var selector = MakeSelector(styleNameHashId, (Style.State)serializedStyle.selector.state);

                // Inheritence
                if(!string.IsNullOrEmpty(serializedStyle.inherit.name))
                {
                    var inheritNameHashId = Style.StringToHash(serializedStyle.inherit.name);

                    // If both states are "All" then inherit each state individually
                    if (serializedStyle.selector.state == -1 && serializedStyle.inherit.state == -1)
                    {
                        foreach (var state in Enum.GetValues(typeof(Style.State)) as Style.State[])
                            SetBaseSelector(MakeSelector(styleNameHashId, state), MakeSelector(inheritNameHashId, state));
                    }

                    // If no state was specified for the selector then set all states for this selector to the same base state 
                    else if (serializedStyle.selector.state == -1)
                    {
                        var baseSelector = MakeSelector(inheritNameHashId, (Style.State)serializedStyle.inherit.state);
                        foreach (var state in Enum.GetValues(typeof(Style.State)) as Style.State[])
                            SetBaseSelector(MakeSelector(styleNameHashId, state), baseSelector);
                    }

                    // If no state was specified for the base then use the normal state
                    else if (serializedStyle.inherit.state == -1)
                        SetBaseSelector(selector, MakeSelector(inheritNameHashId, Style.State.Normal));
                    
                    // Inerit one state from another..
                    else
                        SetBaseSelector(selector, MakeSelector(inheritNameHashId, (Style.State)serializedStyle.inherit.state));
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

