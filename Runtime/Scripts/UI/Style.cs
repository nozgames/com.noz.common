using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NoZ.UI
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

        [Tooltip("Optional identifier of the style that this style is based on")]
        [SerializeField] private string _inherit= null;

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

        /// <summary>
        /// Hash of the inherit identifier
        /// </summary>
        internal int inheritHash { get; private set; }


        private StyleSheet _activeSheet = null;

        public Style parent => null;

        private List<Style> _children;

        public bool isLinked { get; private set; }

        /// <summary>
        /// Return the current state of the style
        /// </summary>
        public State state => _stateProvider != null ? _stateProvider.state : (_parent != null ? _parent.state : State.Normal);

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

            // Find the active style sheet.
            _activeSheet = _styleSheet;

            // Find the active style sheet from our parent?
            for (var parent = _parent; _activeSheet == null && parent != null; parent = parent.parent)
                _activeSheet = parent._activeSheet;

            // If we have a style sheet then propegate it to our children
            if (_activeSheet != null)
                PropegateStyleSheet();
        }

        private void PropegateStyleSheet()
        {
            if (_children == null)
                return;

            foreach (var child in _children)
            {
                if(child._styleSheet == null)
                {
                    child._activeSheet = _activeSheet;
                    child.PropegateStyleSheet();
                }
            }

            Apply();
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

        public static int StringToHash(string name) => Animator.StringToHash(name.ToLower());

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

            _stateProviders.Add(new StateProviderInfo { componentType = typeof(ComponentType), providerType = typeof(ProviderType) });
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
            GetOrCreateStyleTargetInfo(typeof(TargetType)).properties.Add(
                new StyleTargetPropertyInfo<TargetType, PropertyType>
                {
                    propertyInfo = propertyInfo,
                    thunkApply = StyleTargetPropertyInfo<TargetType, PropertyType>.ThunkApply,
                    apply = apply
                });
        }

        static Style ()
        {
            // Register state providers
            RegisterStateProvider<SelectableStateProvider, UnityEngine.UI.Selectable>();

            // Property types
            RegisterPropertyType((s) => float.TryParse(s, out var value) ? value : 0.0f);
            RegisterPropertyType((s) => bool.TryParse(s, out var value) ? value : false);
            RegisterPropertyType((s) => s);
            RegisterPropertyType((s) => ColorUtility.TryParseHtmlString(s, out var value) ? value : Color.white);

            // Properties
            RegisterProperty("color", Color.white);
            RegisterProperty("scale", 1.0f);

            // RectTransform
            RegisterTargetProperty<RectTransform,float>("scale", (t, v) => { t.localScale = new Vector3(v, v, v); });

            // Image
            RegisterTargetProperty<UnityEngine.UI.Image,Color>("color", (i, v) => { i.color = v; });

            // Text
            RegisterTargetProperty<UnityEngine.UI.Text, Color>("color", (c, v) => { c.color = v; });

            // TextMeshPro
            RegisterTargetProperty<TMPro.TextMeshProUGUI, Color>("color", (c, v) => { c.color = v; });
        }
    }
}
