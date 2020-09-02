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
using System.Reflection;
using System.Collections.Generic;

using NoZ.Reflection;

using UnityEngine;

namespace NoZ
{
    internal class StateMachineInfo
    {
        /// <summary>
        /// Cache of state machine info for each type it is used on
        /// </summary>
        private static Dictionary<string, StateMachineInfo> _cache = new Dictionary<string, StateMachineInfo>();

        /// <summary>
        /// All available transitions arranged using (toIndex * (fromIndex * (StateCount+1))
        /// </summary>
        private StateTransitionInfo[] _transitions;

        /// <summary>
        /// Stride of a single row of transitions
        /// </summary>
        private int _transitionStride;

        /// <summary>
        /// Target type 
        /// </summary>
        public Type TargetType { get; private set; }

        /// <summary>
        /// All available states
        /// </summary>
        public StateInfo[] States { get; private set; }

        /// <summary>
        /// Return the state info that matches the given state name
        /// </summary>
        public StateInfo FindState (string name)
        {
            foreach (var state in States)
                if (state.Name == name)
                    return state;

            return null;
        }

        /// <summary>
        /// Returns the index of the transition within the transitions array
        /// </summary>
        private int GetTransitionIndex(int fromIndex, int toIndex) =>
            toIndex * _transitionStride + (fromIndex < 0 ? States.Length : fromIndex);

        /// <summary>
        /// Return the Transition for the given from and to index
        /// </summary>
        public StateTransitionInfo GetTransition(int fromIndex, int toIndex) => _transitions[GetTransitionIndex(fromIndex, toIndex)];

        /// <summary>
        /// Attempt to add a transition to the given transitions list
        /// </summary>
        private static StateTransitionInfo CreateTransition(StateMachineInfo info, StateInfo from, StateInfo to)
        {
            if (from == to)
                return null;

            // Check for the transition method
            var methodInfo = info.TargetType.GetMethod(
                $"On{from?.Name??"Any"}To{to?.Name??"Any"}",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo == null)
                return null;

            return new StateTransitionInfo
            {
                From = from, 
                To = to, 
                InvokeDelegate = OpenDelegate.Create(methodInfo)
            };
        }

        /// <summary>
        /// Create state machine info for a given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static StateMachineInfo Create (Type type, Type statesType)
        {
            var typeName = type.FullName;

            if (_cache.TryGetValue(typeName, out var info))
                return info;

            var values = Enum.GetValues(statesType);
            var states = new StateInfo[values.Length];
            foreach (var value in values)
            {
                var index = (int) value;
                var name = value.ToString();

                if (index < 0 || index >= states.Length)
                    throw new UnityException("State enum must be sequential and start from zero");

                states[index] = new StateInfo { Name = value.ToString(), Mask = (1UL << index), Index = index };
            }

            // Create a new state machine info
            info = new StateMachineInfo();
            info.TargetType = type;
            info.States = states;
            info._transitionStride = states.Length + 1;
            _cache.Add(typeName, info);

            // Get state method and transitions
            info._transitions = new StateTransitionInfo[states.Length * info._transitionStride];
            foreach (var stateToInfo in states)
            {
                // State method?
                var stateMethod = type.GetMethod($"On{stateToInfo.Name}", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (null != stateMethod)
                {
                    var parameters = stateMethod.GetParameters();
                    if (parameters.Length == 0)
                        stateToInfo.InvokeDelegate = OpenDelegate.Create(stateMethod);
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(float))
                        stateToInfo.InvokeWithTimeDelegate = OpenDelegate<float>.Create(stateMethod);
                    else
                        throw new UnityException($"method for state '{stateToInfo.Name}' does not match a valid state signature");
                }

                // Add the "Any" to state transition
                info._transitions[info.GetTransitionIndex(-1,stateToInfo.Index)] = CreateTransition(info, null, stateToInfo);

                // Add transition from all other states
                foreach(var stateFromInfo in states)
                    info._transitions[info.GetTransitionIndex(stateFromInfo.Index, stateToInfo.Index)] = 
                        CreateTransition(info, stateFromInfo, stateToInfo);
            }

            return info;
        }

        /// <summary>
        /// Get a transition from one state to another
        /// </summary>
        public StateTransitionInfo GetTransition(StateInfo from, StateInfo to) =>
            GetTransition(from?.Index??-1, to.Index) ?? GetTransition(-1, to.Index);
    }
}
