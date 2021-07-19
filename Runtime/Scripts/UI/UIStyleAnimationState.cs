using UnityEngine;

namespace NoZ.UI
{
    public class UIStyleAnimationState : StateMachineBehaviour
    {
        public System.Action<int> onStateEnter;

        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            onStateEnter?.Invoke(stateInfo.shortNameHash);
        }
    }

}

