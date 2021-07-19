using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StyledButton : UnityEngine.UI.Button
{
    public StyledButton()
    {

    }



#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Transitions not allowed on styled buttons
        this.transition = Transition.None;
    }
#endif

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        //base.DoStateTransition(state, instant);

        // TODO: here we handle the style

        // TODO: custo editor that hides transition?

        Debug.Log(state);
    }
}
