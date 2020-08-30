﻿/*
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
    public partial class Tween
    {
        /// <summary>
        /// Mark the animation as low priority
        /// </summary>
        public Tween LowPriority() { _flags |= Flags.LowPriority; return this; }

        /// <summary>
        /// Amount of time to wait before playing animation
        /// </summary>
        /// <param name="delay">Delay in seconds</param>
        public Tween Delay(float delay) { _delay = delay; return this; }

        /// <summary>
        /// Add delay to the existing delay amount
        /// </summary>
        public Tween AddDelay(float delay) { _delay += delay; return this; }

        /// <summary>
        /// Set the duration of the animation
        /// </summary>
        /// <param name="duration">Durating in seconds</param>
        public Tween Duration(float duration) { _duration = duration; return this; }

        /// <summary>
        /// Set the target name for the animation. When the animation is started the animation
        /// system will search for a child of the hosting node with the given target name.
        /// </summary>
        /// <param name="target">Name of the target</param>
        public Tween Target(string target) { _targetName = target; return this; }

        /// <summary>
        /// Set the target of a tween ahead of time
        /// </summary>
        /// <param name="target">Target of the tween</param>
        public Tween Target(GameObject target) { _gameObject = target; return this; }

        /// <summary>
        /// Action to run when the animation is finished
        /// </summary>
        /// <param name="onStop">Action</param>
        public Tween OnStop(Action onStop) { _onStop = onStop; return this; }

        /// <summary>
        /// Action to run when the animation starts (after the delay)
        /// </summary>
        /// <param name="onStart">Action</param>
        public Tween OnStart(Action onStart) { _onStart = onStart; return this; }

        /// <summary>
        /// Set the animation update mode
        /// </summary>
        /// <param name="updateMode">New update mode</param>
        public Tween UpdateMode(TweenUpdateMode updateMode) { _updateMode = updateMode; return this; }

        /// <summary>
        /// Set the animation key 
        /// </summary>
        /// <param name="key">Animation key</param>
        public Tween Key(string key) { _key = key; return this; }

        /// <summary>
        /// Set the tween to automatically destroy the target object when the tween is complete
        /// </summary>
        public Tween AutoDestroy(bool autoDestroy = true)
        {
            if (autoDestroy)
                _flags |= Flags.AutoDestroy;
            else
                _flags &= ~Flags.AutoDestroy;

            return this;
        }

        /// <summary>
        /// Set the tween to automatically deactivate the target object when the tween is complete
        /// </summary>
        public Tween AutoDeactivate(bool autoDeactivate = true)
        {
            if (autoDeactivate)
                _flags |= Flags.AutoDeactivate;
            else
                _flags &= ~Flags.AutoDeactivate;

            return this;
        }

        /// <summary>
        /// Enables or disables PingPong mode.  When PingPong mode is enabled the animation
        /// will play itself fully forward and then then in reverse before stopping.  This means that 
        /// the effective duration of the animation will be doubled. Note that everything is run in reverse 
        /// including easing modes.
        /// </summary>
        /// <param name="pingpong">True to enable PingPong mode.</param>
        public Tween PingPong(bool pingpong = true)
        {
            if (pingpong)
                _flags |= Flags.PingPong;
            else
                _flags &= ~Flags.PingPong;

            return this;
        }

        /// <summary>
        /// Set the loop state of the animation
        /// </summary>
        /// <param name="loop">True if the animation should loop</param>
        public Tween Loop(int count = -1)
        {
            _flags =_flags | Flags.Loop;
            _loopCount = count;
            return this;
        }

        /// <summary>
        /// Sets the animation to use unscaled time rather than normal scaled time
        /// </summary>
        /// <param name="unscaled">True to use unscaled time</param>
        public Tween UnscaledTime(bool unscaled = true)
        {
            _flags = unscaled ? (_flags | Flags.UnscaledTime) : (_flags & ~Flags.UnscaledTime);
            return this;
        }

        /// <summary>
        /// Adds a child to a sequence or group
        /// </summary>
        /// <param name="child">child to add</param>
        public Tween Child(Tween child)
        {
            if (child._parent != null)
                throw new ArgumentException("Child already has a parent");

            child.SetParent(this);
            return this;
        }

        /// <summary>
        /// Cancel a tween
        /// </summary>
        public void Cancel ()
        {
            FreeTween(this);
        }
    }
}
