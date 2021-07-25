using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace NoZ.UI
{
    public class UIStyleSheet : ScriptableObject
    {
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
            public UIStyle.State state;
            public SerializedProperty[] properties;
        }

        [SerializeField] private SerializedStyle[] _styles;

        private abstract class PropertyDefinition
        {
            public string name;
            public int nameHashId;
            public TargetDefinition targetDef;

            public abstract PropertyValue Parse(string value);
            public abstract void Apply(UIStyleSheet sheet, UIStyle style, MonoBehaviour monoBehaviour);
        }

        private class ColorPropertyDefinition : PropertyDefinition
        {
            public Action<MonoBehaviour, Color> apply;

            public override PropertyValue Parse(string value)
            {
                if (!ColorUtility.TryParseHtmlString(value, out var color))
                    throw new FormatException($"invalid color format '{value}`");

                return new ColorPropertyValue { value = color };
            }

            public override void Apply(UIStyleSheet sheet, UIStyle style, MonoBehaviour monoBehaviour)
            {
                apply(monoBehaviour, sheet.GetColor(style, nameHashId));
            }
        }

        private class TargetDefinition
        {
            public Type type;
            public List<PropertyDefinition> propertyDefs;
        }

        private static Dictionary<Type, TargetDefinition> _targetDefinitions = new Dictionary<Type, TargetDefinition>();
        private static Dictionary<int, PropertyDefinition> _propertyDefinitions = new Dictionary<int, PropertyDefinition>();

        private static TargetDefinition GetOrCreateTargetDefinition (Type targetType)
        {
            if (!_targetDefinitions.TryGetValue(targetType, out var targetDef))
            {
                targetDef = new TargetDefinition { type = targetType };
                targetDef.propertyDefs = new List<PropertyDefinition>();
                _targetDefinitions[targetType] = targetDef;
            }

            return targetDef;
        }

        private static void RegisterProperty (Type targetType, PropertyDefinition propertyDef)
        {
            if (_propertyDefinitions.ContainsKey(propertyDef.nameHashId))
                throw new InvalidOperationException("Duplicate propety names are not allowed");

            var targetDef = GetOrCreateTargetDefinition(targetType);
            targetDef.propertyDefs.Add(propertyDef);
            _propertyDefinitions[propertyDef.nameHashId] = propertyDef;
        }

        public static void RegisterColorProperty (Type targetType, string name, Action<MonoBehaviour,Color> apply)
        {
            RegisterProperty(targetType, new ColorPropertyDefinition { name = name, nameHashId = UIStyle.StringToHash(name), apply = apply });
        }

        public static event Action onReload;

        public static void ReloadAll ()
        {
            onReload?.Invoke();
        }

        private abstract class PropertyValue
        {
        }

        private class ColorPropertyValue : PropertyValue
        {
            public Color value;
        }

        private class StringPropertyValue : PropertyValue
        {
            public string value;
        }

        private Dictionary<int, Dictionary<ulong, PropertyValue>> _properties;

        private static ulong MakeSelector (int hash, UIStyle.State state) => ((ulong)hash) + (((ulong)state) << 32);

        private T Search<T> (Dictionary<ulong, PropertyValue> properties, int hash, int baseHash, UIStyle.State state) where T : PropertyValue
        {
            var key = MakeSelector(hash, state);
            if (properties.TryGetValue(key, out var property))
                return property as T;

            // Check base if there is one
            if (baseHash != 0)
            {
                var baseProperty = Search<T>(properties, baseHash, 0, state);
                if (null != baseProperty)
                    return baseProperty;
            }

            if (state == UIStyle.State.SelectedHover || state == UIStyle.State.SelectedPressed)
                return Search<T>(properties, hash, baseHash, UIStyle.State.Selected);

            // If the state wasnt normal check normal too if we cant find a state
            if (state != UIStyle.State.Normal)
                return Search<T>(properties, hash, baseHash, UIStyle.State.Normal);

            // Create null property
            properties.Add(key, null);

            return null;
        }

        private T Search<T>(UIStyle style, int propertyId) where T : PropertyValue
        {
            if (null == _properties)
                BuildPropertyDictionary();

            if (!_properties.TryGetValue(propertyId, out var properties))
                return null;

            return Search<T>(properties, style.styleIdHash, style.baseIdHash, style.state);            
        }

        /// <summary>
        /// Retrieve a color property for the given style and flags
        /// </summary>
        public Color GetColor(UIStyle style, int propertyId)
        {
            var property = Search<ColorPropertyValue>(style, propertyId);
            if (null == property)
            {
                if (style.parent != null)
                    return GetColor(style.parent, propertyId);

                return Color.white;
            }

            return property.value;
        }

        /// <summary>
        /// Retrieve a property
        /// </summary>
        /// <param name="style"></param>
        /// <param name="propertyId"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public string GetString(UIStyle style, int propertyId)
        {
            var property = Search<StringPropertyValue>(style, propertyId);
            if (null == property)
            {
                if (style.parent != null)
                    return GetString(style.parent, propertyId);

                return "";
            }

            return property.value;
        }

        private static Regex ParseRegex = new Regex(@"(\w[\w_-]*|\#\w[\w\d_\-\:]*|{|}|;|:|\d+|\#[\dAaBbCcDdEeFf]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        public static UIStyleSheet Parse(string text)
        {
            var tokens = ParseRegex.Matches(text);
            if (tokens.Count == 0)
                return null;

            try
            {
                var sheet = CreateInstance<UIStyleSheet>();

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
            var state = UIStyle.State.Normal;

            var colon = name.IndexOf(':');
            if (colon != -1)
            { 
                var stateName = name.Substring(colon + 1).ToLower();
                name = name.Substring(0, colon);

                if (stateName == "hover")
                    state = UIStyle.State.Hover;
                else if (stateName == "disabled")
                    state = UIStyle.State.Hover;
                else if (stateName == "pressed")
                    state = UIStyle.State.Hover;
                else if (stateName == "selected")
                    state = UIStyle.State.Selected;
                else if (stateName == "selected:hover")
                    state = UIStyle.State.SelectedHover;
                else if (stateName == "selected:pressed")
                    state = UIStyle.State.SelectedPressed;
            }

            serializedStyle.name = name;
            serializedStyle.state = state;

            return MakeSelector(UIStyle.StringToHash(name), state);
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

        public void Apply(UIStyle style)
        {
            foreach(var target in style.targets)
            {
                if (!_targetDefinitions.TryGetValue(target.GetType(), out var targetDef))
                    continue;

                // Apply all properties for the target
                foreach(var propertyDef in targetDef.propertyDefs)
                    propertyDef.Apply(this, style, target);
            }
        }

        private void BuildPropertyDictionary()
        {
            // Populate the dictionary from the serialized values
            _properties = new Dictionary<int, Dictionary<ulong, PropertyValue>>();

            if (_styles == null)
                return;

            foreach (var serializedStyle in _styles)
            {
                var styleNameHashId = UIStyle.StringToHash(serializedStyle.name);
                var selector = MakeSelector(styleNameHashId, serializedStyle.state);

                foreach (var serializedProperty in serializedStyle.properties)
                {
                    var propertyNameHashId = UIStyle.StringToHash(serializedProperty.name);
                    if (!_properties.TryGetValue(propertyNameHashId, out var selectors))
                    {
                        selectors = new Dictionary<ulong, PropertyValue>();
                        _properties[propertyNameHashId] = selectors;
                    }

                    if (!_propertyDefinitions.TryGetValue(propertyNameHashId, out var propertyDef))
                    {
                        Debug.LogWarning($"Unknown property \"{name}\" in style sheet");
                        return;
                    }

                    var property = propertyDef.Parse(serializedProperty.value);
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

