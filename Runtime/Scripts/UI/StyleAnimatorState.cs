using UnityEngine;

namespace NoZ.UI
{
    public class StyleAnimatorState : StateMachineBehaviour
    {
        public System.Action<int> onStateEnter;

        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            onStateEnter?.Invoke(stateInfo.shortNameHash);
        }
    }
}

