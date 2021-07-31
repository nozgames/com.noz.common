using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// TODO: id = (parent with provider name) - (name)

namespace NoZ.Style
{
    public class Style : MonoBehaviour
    {
        public enum State
        {
            Normal,
            Hover,
            Pressed,
            Selected,
            SelectedHover,
            SelectedPressed,
            Disabled
        }

        [Tooltip("Optional style sheet style and all children")]
        [SerializeField] private StyleSheet _styleSheet = null;

        [Tooltip("Identifier of the style")]
        [SerializeField] private string _id = null;

        /// <summary>
        /// Global list of register state providers
        /// </summary>
        private static List<StateProviderInfo> _stateProviders = new List<StateProviderInfo>();

        /// <summary>
        /// Global list of registered target components
        /// </summary>
        private static Dictionary<Type, StyleTargetInfo> _targetInfos = new Dictionary<Type, StyleTargetInfo>();

        /// <summary>
        /// Global list of registered properties
        /// </summary>
        internal static Dictionary<int, StylePropertyInfo> _propertyInfos = new Dictionary<int, StylePropertyInfo>();

        /// <summary>
        /// State provider for this style
        /// </summary>
        private StateProvider _stateProvider;

        /// <summary>
        /// Parent style for all styles that do not have providers
        /// </summary>
        private Style _parent = null;

        /// <summary>
        /// Style targets
        /// </summary>
        // TODO: can we store the target info with this to prevent needing a lookup?
        private Component[] _targets;

        /// <summary>
        /// Hash of the style identifier
        /// </summary>
        internal int idHash { get; private set; }


        private StyleSheet _activeSheet = null;

        public Style parent => null;

        private List<Style> _children;

        public bool isLinked { get; private set; }

        /// <summary>
        /// Return the current state of the style
        /// </summary>
        public State state => _stateProvider != null ? _stateProvider.state : (_parent != null ? _parent.state : State.Normal);

        public StyleSheet styleSheet
        {
            get => _styleSheet;
            set
            {
                if (_styleSheet == value)
                    return;

                _styleSheet = value;

                if (isLinked)
                    UpdateActiveStyleSheet();
            }
        }

        public void Attach()
        {
            if (_stateProvider != null)
            {
                _stateProvider.onStateChanged -= OnStateChanged;
                Destroy(_stateProvider);
                _stateProvider = null;
            }

            // Search for a provider
            foreach (var provider in _stateProviders)
            {
                if(gameObject.TryGetComponent(provider.componentType, out var component))
                {
                    _stateProvider = gameObject.AddComponent(provider.providerType) as StateProvider;
                    _stateProvider.Attach(component);
                    _stateProvider.onStateChanged += OnStateChanged;
                    break;
                }
            }

            // Search for targets
            _targets = GetComponents<Component>().Where(t => _targetInfos.TryGetValue(t.GetType(), out var value)).ToArray();
        }

        private void OnStateChanged(StateProvider provider)
        {
            if (provider == _stateProvider)
                Apply(recurseChildren:true);
        }

        private void LinkToParent ()
        {
            if (isLinked)
                return;

            // Search for the next parent up the chain
            _parent = transform.parent != null ? transform.parent.GetComponentInParent<Style>() : null;
            if (null != _parent)
            {
                if (null == _parent._children)
                    _parent._children = new List<Style>();

                _parent._children.Add(this);
            }

            isLinked = true;

            UpdateActiveStyleSheet();
        }

        private void UpdateActiveStyleSheet()
        {
            // Find the active style sheet.
            _activeSheet = _styleSheet;

            // Find the active style sheet from our parent?
            for (var parent = _parent; _activeSheet == null && parent != null; parent = parent.parent)
                _activeSheet = parent._activeSheet;

            // If we have a style sheet then propegate it to our children
            if (_activeSheet != null && null != _children)
                foreach (var child in _children)
                    child.SetActiveStyleSheet(_activeSheet);

            Apply();
        }

        private void SetActiveStyleSheet(StyleSheet activeSheet)
        {
            // If we have our own style sheet then stop the chain
            if (_styleSheet != null)
                return;

            // Set the sheet from the parent and apply the chanes
            _activeSheet = activeSheet;
            Apply();

            // If we have children we need to push the sheet up to them as well
            if (_children == null)
                return;

            foreach (var child in _children)
                if(child._styleSheet == null)
                    child.SetActiveStyleSheet(activeSheet);
        }

        private void UnlinkFromParent ()
        {
            if (!isLinked)
                return;

            if (_parent != null)
                _parent._children.Remove(this);

            isLinked = false;
        }

        private void OnTransformParentChanged()
        {
            UnlinkFromParent();
            LinkToParent();
            Apply();
        }

        private void OnEnable()
        {
            idHash = StringToHash(string.IsNullOrEmpty(_id) ? name : _id);

            Attach();
            LinkToParent();
            Apply();

            StyleSheet.onReload += OnReload;
        }

        private void OnDisable()
        {
            StyleSheet.onReload -= OnReload;
        }

        private void OnReload ()
        {
            Attach();
            Apply();
        }

        public void Apply (bool recurseChildren=false)
        {
            if (null == _targets || null == _activeSheet)
                return;

            foreach(var target in _targets)
            { 
                if (!_targetInfos.TryGetValue(target.GetType(), out var targetDef))
                    continue;

                // Apply all properties for the target
                foreach (var propertyDef in targetDef.properties)
                    propertyDef.Apply(_activeSheet, this, target);
            }

            // Apply to all children as well.  This is generally done when a state provider changes.
            if (recurseChildren && _children != null)
                foreach (var child in _children)
                    child.Apply(child._stateProvider == null);
        }

        public static int StringToHash(string name) => string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name.ToLower());

        private static StyleTargetInfo GetOrCreateStyleTargetInfo(Type targetType)
        {
            if (_targetInfos.TryGetValue(targetType, out var targetDef))
                return targetDef;

            targetDef = new StyleTargetInfo { type = targetType };
            targetDef.properties = new List<StyleTargetPropertyInfo>();
            _targetInfos[targetType] = targetDef;
            return targetDef;
        }

        /// <summary>
        /// Register a state style system state provider that attaches to a known component on the game object
        /// that the style is attached to.
        /// </summary>
        /// <typeparam name="ProviderType">Type of the provider</typeparam>
        /// <typeparam name="ComponentType">Type of the component to attach to</typeparam>
        public static void RegisterStateProvider<ProviderType,ComponentType>() where ProviderType : Component
        {
            if (_stateProviders.Any(p => p.componentType == typeof(ComponentType)))
                throw new InvalidOperationException("only one provider can be registered for any component type");

            var providerInfo = new StateProviderInfo { componentType = typeof(ComponentType), providerType = typeof(ProviderType) };
            
            // Check to see if the state provider component is derived from any of the providers in the list 
            // and if so make sure it is in the list first so it will be chosen over the base.
            for (int i=0; i < _stateProviders.Count; i++)
            {
                if(_stateProviders[i].componentType.IsAssignableFrom(typeof(ComponentType)))
                {
                    _stateProviders.Insert(i, providerInfo);
                    return;
                }
            }

            // Add to the end
            _stateProviders.Add(providerInfo);
        }

        /// <summary>
        /// Register a property type with the style system by providing a method that can be used to parse
        /// a property value from a string to the property type.
        /// </summary>
        /// <typeparam name="PropertyType">Type of the property</typeparam>
        /// <param name="parse">Method used to parse a property</param>
        public static void RegisterPropertyType<PropertyType>(Func<string, PropertyType> parse)
        {
            if (null == parse)
                throw new ArgumentNullException("parse");

            // Can only register once
            if (StylePropertyInfo<PropertyType>.parse != null)
                throw new InvalidOperationException($"Property type \"{typeof(PropertyType)}\" is already registered");

            StylePropertyInfo<PropertyType>.parse = parse;
        }

        /// <summary>
        /// Register a property with the style system and provide a default value for the property.
        /// </summary>
        /// <typeparam name="PropertyType">Type of the property</typeparam>
        /// <param name="name">Name of the property</param>
        /// <param name="defaultValue">Default value for the property</param>
        public static void RegisterProperty<PropertyType>(string name, PropertyType defaultValue)
        {
            // Make sure the property isnt already registered
            var nameHashId = StringToHash(name);
            if (_propertyInfos.ContainsKey(nameHashId))
                throw new InvalidOperationException("Duplicate propety names are not allowed");

            // Create the property and add it to the global property info dictionary
            var propertyInfo = new StylePropertyInfo<PropertyType>
            {
                name = name,
                nameHashId = nameHashId,
                defaultValue = defaultValue,
                thunkParse = StylePropertyInfo<PropertyType>.ThunkParse
            };

            _propertyInfos[nameHashId] = propertyInfo;
        }

        /// <summary>
        /// Register a property for a specific component
        /// </summary>
        /// <typeparam name="TargetType">Target component type</typeparam>
        /// <typeparam name="PropertyType">Target property type</typeparam>
        /// <param name="name">Name of the property</param>
        /// <param name="apply">Method to use to apply the property value to the target</param>
        public static void RegisterTargetProperty<TargetType, PropertyType>(string name, Action<TargetType, PropertyType> apply) where TargetType : Component
        {
            var nameHashId = StringToHash(name);
            if(!_propertyInfos.TryGetValue(nameHashId, out var propertyInfo))
                throw new InvalidOperationException($"Unknown property \"{name}\"");

            // Create the property and add it to the global property info dictionary           
            GetOrCreateStyleTargetInfo(typeof(TargetType)).AddProperty(propertyInfo, apply);
        }

        static Style ()
        {
            // Register state providers
            RegisterStateProvider<SelectableStateProvider, UnityEngine.UI.Selectable>();
            RegisterStateProvider<ToggleStateProvider, UnityEngine.UI.Toggle>();

            // Property types
            RegisterPropertyType((s) => float.TryParse(s, out var value) ? value : 0.0f);
            RegisterPropertyType((s) => bool.TryParse(s, out var value) ? value : false);
            RegisterPropertyType((s) => s);
            RegisterPropertyType((s) => ColorUtility.TryParseHtmlString(s, out var value) ? value : Color.white);

            // Properties
            RegisterProperty("color", Color.white);
            RegisterProperty("scale", 1.0f);
            RegisterProperty("font-bold", false);
            RegisterProperty("font-italic", false);
            RegisterProperty("font-underline", false);
            RegisterProperty("font-size", 1.0f);

            // RectTransform
            RegisterTargetProperty<RectTransform,float>("scale", (t, v) => { t.localScale = new Vector3(v, v, v); });

            // Image
            RegisterTargetProperty<UnityEngine.UI.Image,Color>("color", (i, v) => { i.color = v; });

            // RawImage
            RegisterTargetProperty<UnityEngine.UI.RawImage, Color>("color", (i, v) => { i.color = v; });

            // Text
            RegisterTargetProperty<UnityEngine.UI.Text, Color>("color", (c, v) => { c.color = v; });

            // TextMeshPro
            RegisterTargetProperty<TMPro.TextMeshProUGUI, Color>("color", (c, v) => { c.color = v; });
            RegisterTargetProperty<TMPro.TextMeshProUGUI, bool>("font-bold", (c, v) => { c.fontWeight = v ? TMPro.FontWeight.Bold : TMPro.FontWeight.Regular; });
            RegisterTargetProperty<TMPro.TextMeshProUGUI, bool>("font-italic", (c, v) => { if (v) c.fontStyle |= TMPro.FontStyles.Italic; else c.fontStyle &= ~(TMPro.FontStyles.Italic); });
            RegisterTargetProperty<TMPro.TextMeshProUGUI, bool>("font-underline", (c, v) => { if (v) c.fontStyle |= TMPro.FontStyles.Underline; else c.fontStyle &= ~(TMPro.FontStyles.Underline); });
            RegisterTargetProperty<TMPro.TextMeshProUGUI, float>("font-size", (c, v) => { c.fontSize = v; });

            RegisterTargetProperty<TMPro.TMP_InputField, Color>("color", (c, v) => { c.selectionColor = v; });
        }
    }
}
