using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NoZ.UI
{
    public class UIStyle : UIBehaviour, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Optional style sheet style and all children")]
        [SerializeField] private UIStyleSheet _styleSheet = null;

        private UIBehaviour[] _behaviours;

        private UnityEngine.UI.Selectable _selectable;

        private UIStyle _parent = null;

        private bool _hover = false;
        private bool _pressed = false;
        private bool _selected = false;

        public bool isSelected => _selected || (_parent != null ? _parent.isSelected : false);
        public bool isHover => _hover || (_parent != null ? _parent.isHover : false);
        public bool isPressed => _pressed || (_parent != null ? _parent.isPressed : false);

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            _parent = transform.parent.GetComponentInParent<UIStyle>();            
        }

        protected override void OnEnable()
        {
            base.OnEnable();            

            var button = GetComponent<UnityEngine.UI.Button>();
            if(button != null)
                button.onClick.AddListener(() => Debug.Log("Click"));

            _parent = transform.parent.GetComponentInParent<UIStyle>();

            _behaviours = GetComponents<UIBehaviour>();

            UpdateProperties();

            UIStyleSheet.onReload += UpdateProperties;

            HookSelectable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            UIStyleSheet.onReload -= UpdateProperties;
        }

        private void UpdateProperties ()
        {
            if (null == _behaviours)
                return;

            foreach (var s in GetComponentsInChildren<UIStyle>())
                if(s != this)
                    s.UpdateProperties();

            foreach(var behaviour in _behaviours)
            {
                if (behaviour is UnityEngine.UI.Image image)
                    image.color = _styleSheet.GetColor(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            UpdateProperties();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            UpdateProperties();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                _pressed = true;
                UpdateProperties();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _pressed = false;
                UpdateProperties();
            }
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
            _hover = false;
            _pressed = false;            

            if (hash == Animator.StringToHash(_selectable.animationTriggers.highlightedTrigger))
                _hover = true;
            else if (hash == Animator.StringToHash(_selectable.animationTriggers.pressedTrigger))
                _pressed = true;

            UpdateProperties();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            UpdateProperties();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            UpdateProperties();
        }
    }
}
