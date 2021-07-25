using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NoZ.UI
{
    public class Style : UIBehaviour
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


        private Style _parent = null;
        private StateProvider _stateProvider;
        private Component[] _targets;

        public int idHash { get; private set; }

        public int inheritHash { get; private set; }

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
                _stateProvider.onStateChanged -= OnStateChanged;

            idHash = StringToHash(_id);
            inheritHash = StringToHash(_inherit);

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
                Apply();
        }

        private void LinkToParent ()
        {
            if (isLinked)
                return;

            _parent = transform.parent.GetComponentInParent<Style>();
            if (null != _parent)
            {
                if (null == _parent._children)
                    _parent._children = new List<Style>();

                _parent._children.Add(this);
            }

            isLinked = true;

            // Find the active style sheet.
            _activeSheet = _styleSheet;
            var parent = _parent;
            while (_activeSheet == null && parent != null)
                _activeSheet = parent._activeSheet;
        }

        private void UnlinkFromParent ()
        {
            if (!isLinked)
                return;

            if (_parent != null)
                _parent._children.Remove(this);

            isLinked = false;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            UnlinkFromParent();
            LinkToParent();
            Apply();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            Attach();
            LinkToParent();
            Apply();

            StyleSheet.onReload += Attach;
            StyleSheet.onReload += Apply;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            StyleSheet.onReload -= Attach;
            StyleSheet.onReload -= Apply;
        }

        public void Apply ()
        {
            if (null == _targets)
                return;

            foreach(var target in _targets)
            { 
                if (!_targetInfos.TryGetValue(target.GetType(), out var targetDef))
                    continue;

                // Apply all properties for the target
                foreach (var propertyDef in targetDef.properties)
                    propertyDef.Apply(_styleSheet, this, target);
            }
        }

        public static void RegisterStateProvider<T>(Type componentType) where T : Component
        {
            if (_stateProviders.Any(p => p.componentType == componentType))
                throw new InvalidOperationException("only one provider can be registered for any component type");

            _stateProviders.Add(new StateProviderInfo { componentType = componentType, providerType = typeof(T) });
        }

        public static int StringToHash(string name) => Animator.StringToHash(name);

        private static StyleTargetInfo GetOrCreateStyleTargetInfo(Type targetType)
        {
            if (_targetInfos.TryGetValue(targetType, out var targetDef))
                return targetDef;

            targetDef = new StyleTargetInfo { type = targetType };
            targetDef.properties = new List<StylePropertyInfo>();
            _targetInfos[targetType] = targetDef;
            return targetDef;
        }

        public static void RegisterPropertyParser<T>(Func<string, T> parse)
        {
            StylePropertyInfo<T>.parse = parse;
        }

        public static void RegisterProperty<T>(Type targetType, string name, Action<Component, T> apply, T defaultValue)
        {
            // Make sure the property isnt already registered
            var nameHashId = Style.StringToHash(name);
            if (_propertyInfos.ContainsKey(nameHashId))
                throw new InvalidOperationException("Duplicate propety names are not allowed");

            // Create the property and add it to the global property info dictionary
            var propertyInfo = new StylePropertyInfo<T>
            {
                name = name,
                nameHashId = nameHashId,
                thunkApply = StylePropertyInfo<T>.ThunkApply,
                thunkParse = StylePropertyInfo<T>.ThunkParse,
                apply = apply,
                defaultValue = defaultValue
            };
            _propertyInfos[nameHashId] = propertyInfo;

            // Add the property to the target type
            GetOrCreateStyleTargetInfo(targetType).properties.Add(propertyInfo);
        }

        static Style ()
        {
            // Register state providers
            RegisterStateProvider<SelectableStateProvider>(typeof(UnityEngine.UI.Selectable));

            // Register parser
            RegisterPropertyParser<float>((s) => float.TryParse(s, out var value) ? value : 0.0f);
            RegisterPropertyParser<string>((s) => s);
            RegisterPropertyParser<Color>((s) => ColorUtility.TryParseHtmlString(s, out var value) ? value : Color.white);

            // RectTransform
            RegisterProperty<float>(typeof(RectTransform), "scale", (c, v) => { (c as Transform).localScale = new Vector3(v, v, v); }, 1.0f);

            // Image
            RegisterProperty<Color>(typeof(UnityEngine.UI.Image), "image-color", (c, v) => { (c as UnityEngine.UI.Image).color = v; }, Color.white);
        }
    }
}
