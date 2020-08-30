/*
  NoZ Unity Library

  Copyright(c) 2019 NoZ Games, LLC

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files(the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions :

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

using System;
using UnityEngine;

namespace NoZ
{
    public struct StateMachine<TState> where TState : Enum
    {
        private StateMachineInfo _sminfo;
        private StateInfo _stateInfo;
        private ulong _stateMask;
        private UnityEngine.Object _target;
        private TState _state;

        public TState State {
            get => _state;
            set {
                var stateInfo = _sminfo.States[Convert.ToInt32(value)];
                if (null == stateInfo)
                    throw new ArgumentException("unknown state");

                // Ignore calls to set state to the current state
                if (stateInfo == _stateInfo)
                    return;

                // If the state has already been set in this recursion then break the loop
                if ((_stateMask & stateInfo.Mask) != 0)
                {
                    Debug.LogWarning($"StateMachine.SetState: {stateInfo.Name}: recursive call to SetState or call from within transition ignored");
                    return;
                }

                var oldStateMask = _stateMask;
                var oldStateInfo = _stateInfo;

                // Switch the state
                PreviousState = _state;
                PreviousStateTime = StateTime;
                StateTime = 0.0f;
                _stateInfo = stateInfo;
                _stateMask |= stateInfo.Mask;
                _state = value;

#if false
                Debug.Log($"StateMachine.State: {oldStateInfo?.Name ?? "None"} -> {stateInfo.Name}");
#endif

                // Execute the transition
                var transition = _sminfo.GetTransition(oldStateInfo, stateInfo);
                if (transition != null)
                    transition.Invoke(_target);

                // Update the state immeidately if the transition didnt change the state
                if (stateInfo == _stateInfo)
                    Update(0.0f);

                // Return the state mask to zero if it was original zero.  We do this because the SetState call
                // where statemask was zero was the top level call in a possible recurssion.
                if (oldStateMask == 0)
                    _stateMask = 0;
            }
        }

        /// <summary>
        /// Elapsed time in the current state
        /// </summary>
        public float StateTime { get; private set; }

        /// <summary>
        /// Previous state the 
        /// </summary>
        public TState PreviousState { get; private set; }

        /// <summary>
        /// Amount of time in the previous state
        /// </summary>
        public float PreviousStateTime { get; private set; }

        /// <summary>
        /// Construct a new state machine
        /// </summary>
        /// <param name="target"></param>
        /// <param name="initialState"></param>
        public StateMachine (UnityEngine.Object target, TState initialState)
        {
            _sminfo = StateMachineInfo.Create(target.GetType(), typeof(TState));
            _target = target;
            _stateInfo = null;            
            _stateMask = 0;
            _state = initialState;
            StateTime = 0.0f;
            PreviousState = initialState;
            PreviousStateTime = 0.0f;
            State = initialState;
        }

        /// <summary>
        /// Call the state update method for the current state and handle any state transitions
        /// </summary>
        public void Update() => Update(Time.deltaTime);

        private void Update(float deltaTime)
        {
            if (_target == null)
                return;

            StateTime += deltaTime;

            // Allow any state to be transitioned to once
            _stateMask = 0;

            // Exeucte the current state
            _stateInfo.Invoke(_target, StateTime);
        }
    }
}
