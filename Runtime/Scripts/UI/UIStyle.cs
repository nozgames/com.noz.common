using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NoZ.UI
{
    public class UIStyle : UIBehaviour
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
        [SerializeField] private UIStyleSheet _styleSheet = null;

        [Tooltip("Identifier of the style")]
        [SerializeField] private string _id = null;

        [Tooltip("Optional identifier of the style that this style is based on")]
        [SerializeField] private string _inherit= null;

        private static List<ControllerDefinition> _controllerDefinitions = new List<ControllerDefinition>();

        private UIStyle _parent = null;
        private UIStateController2 _stateController;


        public int idHash { get; private set; }

        public int inheritHash { get; private set; }

        private UIStyleSheet _activeSheet = null;

        public UIStyle parent => null;

        public List<UIStyle> _children;

        public bool isLinked { get; private set; }

        public State state => _stateController != null ? _stateController.state : (_parent != null ? _parent.state : State.Normal);

        private class ControllerDefinition
        {
            public Type componentType;
            public Type controllerType;
        }
        
        public static void RegisterController<T> (Type componentType) where T : Component
        {
            _controllerDefinitions.Add(new ControllerDefinition { componentType = componentType, controllerType = typeof(T) });
        }

        public static int StringToHash(string name) => Animator.StringToHash(name);

        public void Attach()
        {
            idHash = StringToHash(_id);
            inheritHash = StringToHash(_inherit);

            // Scan the known controllers
            foreach (var controllerDef in _controllerDefinitions)
            {
                if(gameObject.TryGetComponent(controllerDef.componentType, out var component))
                {
                    _stateController = gameObject.AddComponent(controllerDef.controllerType) as UIStateController2;
                    _stateController.Attach(component);
                    return;
                }
            }
        }

        private void LinkToParent ()
        {
            if (isLinked)
                return;

            _parent = transform.parent.GetComponentInParent<UIStyle>();
            if (null != _parent)
                _parent._children.Add(this);

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

            UIStyleSheet.onReload += Apply;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            UIStyleSheet.onReload -= Apply;
        }

        public void Apply ()
        {
            _styleSheet.Apply(this);
        }
    }
}
