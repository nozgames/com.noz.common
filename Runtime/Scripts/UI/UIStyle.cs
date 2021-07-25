using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NoZ.UI
{
    public class UIStyle : UIBehaviour, ISelectHandler, IDeselectHandler
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
        [SerializeField] private string _styleId = null;

        [Tooltip("Optional identifier of the style that this style is based on")]
        [SerializeField] private string _styleBase = null;


        private UIBehaviour[] _behaviours;

        private UnityEngine.UI.Selectable _selectable;

        private UIStyle _parent = null;
        private int _styleIdHash = 0;
        private int _styleBaseHash = 0;

        // TODO: style id should be parent + . + _styleId
        public string styleId => _styleId;
        public int styleIdHash => _styleIdHash;

        public string baseId => _styleBase;
        public int baseIdHash => _styleBaseHash;

        private State _animationState = State.Normal;
        private State _state = State.Normal;
        private UIStyleSheet _activeSheet = null;

        private bool _selected = false;

        public UIBehaviour[] targets => _behaviours;

        public UIStyle parent => null;

        public List<UIStyle> _children;

        public bool isLinked { get; private set; }

        public State state => _state;




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

            _styleIdHash = StringToHash(styleId);
            _styleBaseHash = StringToHash(_styleBase);

            var button = GetComponent<UnityEngine.UI.Button>();
            if(button != null)
                button.onClick.AddListener(() => Debug.Log("Click"));

            _parent = transform.parent.GetComponentInParent<UIStyle>();

            _behaviours = GetComponents<UIBehaviour>();

            LinkToParent();
            Apply();

            UIStyleSheet.onReload += Apply;

            HookSelectable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            UIStyleSheet.onReload -= Apply;
        }

        public void HookSelectable ()
        {
            _selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (null == _selectable)
                return;

            var animator = _selectable.animator;
            if (null == _selectable.animator)
                animator = _selectable.gameObject.AddComponent<Animator>();

            _selectable.transition = UnityEngine.UI.Selectable.Transition.None;
            _selectable.transition = UnityEngine.UI.Selectable.Transition.Animation;

            var controller = new AnimatorController();
            controller.name = "UIStyle";
            controller.AddLayer("Base");

            var stateMachine = controller.layers[0].stateMachine;
            AddState(controller, stateMachine, _selectable.animationTriggers.normalTrigger);
            AddState(controller, stateMachine, _selectable.animationTriggers.highlightedTrigger);
            AddState(controller, stateMachine, _selectable.animationTriggers.pressedTrigger);
            AddState(controller, stateMachine, _selectable.animationTriggers.selectedTrigger);
            AddState(controller, stateMachine, _selectable.animationTriggers.disabledTrigger);

            animator.runtimeAnimatorController = controller;

            foreach(var behavior in animator.GetBehaviours<NoZ.UI.UIStyleAnimationState>())
                behavior.onStateEnter = OnAnimationState;
        }

        private void AddState (AnimatorController controller, AnimatorStateMachine stateMachine, string name)
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var state = stateMachine.AddState(name);
            var transition = stateMachine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 1.0f, name);
            transition.duration = 0.0f;
            transition.hasFixedDuration = true;
            state.AddStateMachineBehaviour<UIStyleAnimationState>();
        }

        private void OnAnimationState(int hash)
        {
            _animationState = State.Normal;

            if (hash == Animator.StringToHash(_selectable.animationTriggers.highlightedTrigger))
                _animationState = State.Hover;
            else if (hash == Animator.StringToHash(_selectable.animationTriggers.pressedTrigger))
                _animationState = State.Pressed;
            else if (hash == Animator.StringToHash(_selectable.animationTriggers.disabledTrigger))
                _animationState = State.Disabled;
            else if (hash == Animator.StringToHash(_selectable.animationTriggers.selectedTrigger))
                _animationState = State.Selected;

            UpdateState();
        }

        private void UpdateState()
        {
            if (_selectable == null && _parent != null)
                _state = _parent.state;
            else if (_selected && _animationState == State.Hover)
                _state = State.SelectedHover;
            else if (_selected && _animationState == State.Pressed)
                _state = State.SelectedPressed;
            else
                _state = _animationState;

            foreach(var child in _children)
                child.UpdateState();

            Apply();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            Apply();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            Apply();
        }

        public static int StringToHash(string name) => Animator.StringToHash(name);

        public void Apply ()
        {
            _styleSheet.Apply(this);
        }
    }
}
