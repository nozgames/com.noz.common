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

using UnityEngine;

namespace NoZ
{
    public enum EaseType
    {
        None,
        EaseInBack,
        EaseOutBack,
        EaseInOutBack,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseInBounce,
        EaseOutBounce,
        EaseInOutBounce,
        EaseInElastic,
        EaseOutElastic,
        EaseInOutElastic,
    }

    /// <summary>
    /// Plays an tween on a node
    /// </summary>
    public partial class Tween
    {
        [System.Flags]
        private enum Flags : ushort
        {
            /// <summary>
            /// Indicates the tween should process the children as a sequence rather than a group
            /// </summary>
            Sequence = (1 << 0),

            /// <summary>
            /// Delta time should be unscaled
            /// </summary>
            UnscaledTime = (1 << 1),

            /// <summary>
            /// Loop the tween
            /// </summary>
            Loop = (1 << 2),

            /// <summary>
            /// Indicates the tween has been started 
            /// </summary>
            Started = (1 << 3),

            /// <summary>
            /// Indicates the tween should play itself forward, then backward before stopping
            /// </summary>
            PingPong = (1 << 4),

            /// <summary>
            /// Tween is low priority and can be stopped if necessary
            /// </summary>
            LowPriority = (1 << 5),

            /// <summary>
            /// Tween is active
            /// </summary>
            Active = (1 << 6),

            /// <summary>
            /// Tween is stopping
            /// </summary>
            Stopping = (1 << 7),

            /// <summary>
            /// Automatically destroy the target object when the tween completes
            /// </summary>
            AutoDestroy = (1 << 8),

            /// <summary>
            /// Automatically deactivate the target object when the tween completes
            /// </summary>
            AutoDeactivate = (1<<9)
        }

        private delegate float EasingDelegate(float t, float p1, float p2);
        private delegate bool StartDelegate(Tween tween, GameObject gameObject);
        private delegate void UpdateDelegate(Tween tween, float normalizedTime);

        public static bool IsPaused { get; private set; } = true;

        /// <summary>
        /// GameObject that owns the tween 
        /// </summary>
        private GameObject _gameObject;

        /// <summary>
        /// Target of the tween
        /// </summary>
        private Object _target;

        /// <summary>
        /// Name of the target
        /// </summary>
        private string _targetName;

        /// <summary>
        /// Optional tween key 
        /// </summary>
        private string _key;

        private TweenUpdateMode _updateMode;
        private Vector4 _vectorParam0;
        private Vector4 _vectorParam1;
        private Object _objectParam;
        private UpdateDelegate _delegate;
        private StartDelegate _startDelegate;
        private EasingDelegate _easeDelegate;
        private System.Action _onStop;
        private System.Action _onStart;
        private System.Func<Tween,float,bool> _custom;
        private float _easeParam1;
        private float _easeParam2;
        private float _duration;
        private float _delay;
        private float _elapsed;
        private Tween _next;
        private Tween _prev;
        private Tween _firstChild;
        private Tween _lastChild;
        private Tween _parent;
        private Flags _flags;
        private int _loopCount;

        #region Static

        /// <summary>
        /// Root tween that manages all active tweens
        /// </summary>
        private static Tween _root = new Tween();

        private static int _count = 0;
        private static int _countHighPriority = 0;
        private static int _countLowPriority = 0;

        public static int ActiveCount => _count;
        public static bool IsLowPriorityActive => _countLowPriority > 0;
        public static bool IsHighPriorityActive => _countHighPriority > 0;

        public Object TargetObject => _target;

        public Vector4 Param1 => _vectorParam0;

        public Vector4 Param2 => _vectorParam1;

        /// <summary>
        /// Array of all availalbe easing delegates
        /// </summary>
        private readonly static EasingDelegate[] _easingDelegates;

        /// <summary>
        /// Stop all tweens running on the game object that owns the given behavior
        /// </summary>
        public static void Stop(MonoBehaviour behaviour, bool allowCallbacks = true) => Stop(behaviour.gameObject);

        /// <summary>
        /// Stop all tweens running on the given game object
        /// </summary>
        public static void Stop(GameObject gameObject, bool allowCallbacks=true)
        {
            if (null == gameObject)
                return;

            // Stop in reverse because any new tweens that may be created as a result of the 
            // stop callback being called will be added to the end
            Tween prev;
            for (var tween = _root._lastChild; tween != null; tween = prev)
            {
                prev = tween._prev;
                if (tween._gameObject == gameObject)
                    Stop(tween, allowCallbacks);
            }
        }

        /// <summary>
        /// Stop all tweens assocated with this game object and all children of the game object
        /// </summary>
        /// <param name="gameObject"></param>
        public static void StopAllInChildren (GameObject gameObject, bool allowCallbacks=true)
        {
            if (null == gameObject)
                return;

            // Stop in reverse because any new tweens that may be created as a result of the 
            // stop callback being called will be added to the end
            Tween prev;
            for (var tween = _root._lastChild; tween != null; tween = prev)
            {
                prev = tween._prev;
                if (tween._gameObject == gameObject || tween._gameObject.transform.IsChildOf(gameObject.transform))
                    Stop(tween, allowCallbacks);
            }
        }

        /// <summary>
        /// Stop all tweens running on the game object that owns the given behavior
        /// </summary>
        public static void Stop(MonoBehaviour behaviour, string key, bool allowCallbacks=true) => Stop(behaviour.gameObject,key, allowCallbacks);

        /// <summary>
        /// Stop all tweens on the given node that match the given key.
        /// </summary>
        public static void Stop(GameObject gameObject, string key, bool allowCallbacks=true)
        {
            if (null == gameObject)
                return;

            // Stop in reverse because any new tweens that may be created as a result of the 
            // stop callback being called will be added to the end
            Tween prev;
            for (var tween = _root._lastChild; tween != null; tween = prev)
            {
                prev = tween._prev;
                if (tween._gameObject == gameObject && tween._key != null && tween._key == key)
                    Stop(tween, allowCallbacks);
            }
        }

        /// <summary>
        /// Stop all tweens on all nodes that match the given key
        /// </summary>
        /// <param name="key"></param>
        public static void Stop(string key)
        {
            // Stop in reverse because any new tweens that may be created as a result of the 
            // stop callback being called will be added to the end
            Tween prev;
            for (var tween = _root._lastChild; tween != null; tween = prev)
            {
                prev = tween._prev;
                if (tween._key != null && tween._key == key)
                    Stop(tween);
            }
        }

        /// <summary>
        /// Stop the given tween.
        /// </summary>
        /// <param name="tween"></param>
        private static void Stop(Tween tween, bool allowCallbacks = true)
        {
            if ((tween._flags & Flags.Stopping) == Flags.Stopping)
                return;

            tween._flags |= Flags.Stopping;

            // Stop all children
            while (tween._firstChild != null)
                Stop(tween._firstChild, allowCallbacks);

            tween.SetParent(null);
            tween.IsActive = false;

            var onStop = tween._onStop;
            var gameObject = tween._gameObject;
            var autoDestroy = tween.IsAutoDestroy;
            var autoDeactivate = tween.IsAutoDeactivate;

            // Free the tween
            FreeTween(tween);

            // Call onStop
            if (allowCallbacks && gameObject != null)
                onStop?.Invoke();

            // Auto-destroy the game-object?
            if (null != gameObject)
            {
                if (autoDestroy)
                    Object.Destroy(gameObject);
                else if (autoDeactivate)
                    gameObject.SetActive(false);
            }
        }

        public static void Update(TweenUpdateMode updateMode)
        {
            var elapsedNormal = 0f;
            var elapsedUnscaled = 0f;

            switch (updateMode)
            {
                case NoZ.TweenUpdateMode.FixedUpdate:
                    elapsedNormal = Time.fixedDeltaTime;
                    elapsedUnscaled = Time.fixedUnscaledDeltaTime;
                    break;

                case NoZ.TweenUpdateMode.Update:
                    elapsedNormal = Time.deltaTime;
                    elapsedUnscaled = Time.unscaledDeltaTime;
                    break;

                default:
                    throw new System.NotImplementedException();
            }

            Tween next = null;
            for (var tween = _root._firstChild; tween != null; tween = next)
            {
                next = tween._next;

                if (tween._updateMode != updateMode)
                    continue;

                // Stop the tween if either of its target objects have been destroyed
                if(tween._gameObject == null || tween._target == null) 
                { 
                    Stop(tween);
                    continue;
                }

                if ((tween._flags & Flags.UnscaledTime) == Flags.UnscaledTime)
                    tween.UpdateInternal(elapsedUnscaled);
                else
                    tween.UpdateInternal(elapsedNormal);

                // If the tween is no longer active then stop it
                if (!tween.IsActive)
                    Stop(tween);
            }

            // Pause the tween system if none are running
            if (_root._firstChild == null)
                IsPaused = true;
        }

        /// <summary>
        /// Internal method used to allocate a new tween object from the pool
        /// </summary>
        /// <returns></returns>
        private static Tween AllocTween()
        {
            var tween = (TweenManager.Instance != null && TweenManager.Instance.Pool.Count > 0) ? TweenManager.Instance.Pool.Dequeue() : new Tween();
            tween._delay = 0f;
            tween._updateMode = NoZ.TweenUpdateMode.Update;
            tween._flags = 0;
            tween._elapsed = 0f;
            tween._duration = 1f;
            tween.IsActive = false;
            return tween;
        }

        /// <summary>
        /// Internal method used to Free an tween object and return it to the pool
        /// </summary>
        /// <param name="tween"></param>
        private static void FreeTween(Tween tween)
        {
            tween.IsActive = false;
            tween._flags = 0;
            tween._key = null;
            tween._onStop = null;
            tween._onStart = null;
            tween._custom = null;
            tween._gameObject = null;
            tween._target = null;
            tween._targetName = null;
            tween._easeDelegate = null;
            tween._delegate = null;
            tween._startDelegate = null;
            tween._next = null;
            tween._prev = null;
            tween._firstChild = null;
            tween._lastChild = null;
            tween._parent = null;

            // Return the tween to the pool if there is rool
            if (TweenManager.Instance != null && TweenManager.Instance.Pool.Count < TweenManager.Instance.MaxPoolSize)
                TweenManager.Instance.Pool.Enqueue(tween);
        }

        static Tween()
        {
            var names = System.Enum.GetNames(typeof(EaseType));
            _easingDelegates = new EasingDelegate[names.Length];
            for (int i = 1; i < names.Length; i++)
                _easingDelegates[i] =
                    (EasingDelegate)typeof(Tween).GetMethod(
                        names[i],
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
                        ).CreateDelegate(typeof(EasingDelegate));
        }

#endregion

        public bool IsActive {
            get => (_flags & Flags.Active) == Flags.Active;
            private set {
                if (value == ((_flags & Flags.Active) == Flags.Active))
                    return;

                if (value)
                {
                    _flags |= Flags.Active;
                    _count++;
                    if (IsLowPriority)
                        _countLowPriority++;
                    else
                        _countHighPriority++;
                }
                else
                {
                    _flags &= (~Flags.Active);
                    _count--;
                    if (IsLowPriority)
                        _countLowPriority--;
                    else
                        _countHighPriority--;
                }
            }
        }

        public bool IsLooping => (_flags & Flags.Loop) == Flags.Loop;
        public bool IsPingPong => (_flags & Flags.PingPong) == Flags.PingPong;
        public bool IsSequence => (_flags & Flags.Sequence) == Flags.Sequence;
        public bool IsStarted => (_flags & Flags.Started) == Flags.Started;
        public bool IsLowPriority => (_flags & Flags.LowPriority) == Flags.LowPriority;
        public bool IsAutoDestroy => (_flags & Flags.AutoDestroy) == Flags.AutoDestroy;
        public bool IsAutoDeactivate => (_flags & Flags.AutoDeactivate) == Flags.AutoDeactivate;

        public static Tween Shake(Vector2 positionalIntensity, float rotationalIntensity)
        {
            var tween = AllocTween();
            tween._vectorParam0 = new Vector3(positionalIntensity.x, positionalIntensity.y, rotationalIntensity);
            tween._vectorParam1 = new Vector3(
                Random.Range(0.0f, 100.0f),
                Random.Range(0.0f, 100.0f),
                Random.Range(0.0f, 100.0f));
            tween._delegate = ShakeUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween ShakePosition(Vector2 intensity)
        {
            var tween = AllocTween();
            tween._vectorParam0 = intensity;
            tween._vectorParam1 = new Vector3(
                Random.Range(0.0f, 100.0f),
                Random.Range(0.0f, 100.0f), 0);
            tween._delegate = ShakePositionUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween ShakeRotation(float intensity)
        {
            var tween = AllocTween();
            tween._vectorParam0 = new Vector3(0.0f, 0.0f, intensity);
            tween._vectorParam1 = new Vector3(0.0f, 0.0f, Random.Range(0.0f, 100.0f));
            tween._delegate = ShakeRotationUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Move(Vector2 from, Vector2 to, bool local = true)
        {
            var tween = AllocTween();
            tween._vectorParam0 = from;
            tween._vectorParam1 = to;
            tween._delegate = local ? MoveUpdateDelegate : MoveWorldUpdate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Move(Vector3 from, Vector3 to, bool local = true)
        {
            var tween = AllocTween();
            tween._vectorParam0 = from;
            tween._vectorParam1 = to;
            tween._delegate = local ? MoveUpdateDelegate : MoveWorldUpdate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween MoveTo(Vector2 to)
        {
            var tween = AllocTween();
            tween._vectorParam1 = to;
            tween._delegate = MoveToUpdateDelegate;
            tween._startDelegate = MoveToStartDelegate;
            return tween;
        }

        public static Tween MoveTo(Vector3 to)
        {
            var tween = AllocTween();
            tween._vectorParam1 = to;
            tween._delegate = MoveToUpdateDelegate;
            tween._startDelegate = MoveToStartDelegate;
            return tween;
        }

        public static Tween MoveBy (in Vector2 by)
        {
            var tween = AllocTween();
            tween._vectorParam1 = by;
            tween._delegate = MoveUpdateDelegate;
            tween._startDelegate = MoveByStartDelegate;
            return tween;
        }

        public static Tween MoveBy(in Vector3 by)
        {
            var tween = AllocTween();
            tween._vectorParam1 = by;
            tween._delegate = MoveUpdateDelegate;
            tween._startDelegate = MoveByStartDelegate;
            return tween;
        }

        public static Tween Rotate(float from, float to)
        {
            var tween = AllocTween();
            tween._vectorParam0 = new Vector3(0, 0, from);
            tween._vectorParam1 = new Vector3(0, 0, to);
            tween._delegate = RotateUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Rotate(Vector3 from, Vector3 to)
        {
            var tween = AllocTween();
            tween._vectorParam0 = from;
            tween._vectorParam1 = to;
            tween._delegate = RotateUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Rotate(Quaternion from, Quaternion to, bool local=false)
        {
            var tween = AllocTween();
            tween._vectorParam0 = new Vector4(from.x, from.y, from.z, from.w);
            tween._vectorParam1 = new Vector4(to.x, to.y, to.z, to.w);
            tween._delegate = local ? RotateQuaternionLocalUpdateDelegate : RotateQuaternionUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Scale(in Vector3 from, in Vector3 to)
        {
            var tween = AllocTween();
            tween._vectorParam0 = from;
            tween._vectorParam1 = to;
            tween._delegate = ScaleUpdateDelegate;
            tween._startDelegate = TransformStartDelegate;
            return tween;
        }

        public static Tween Scale(float from, float to) => Scale(new Vector3(from,from,from), new Vector3(to,to,to));

        public static Tween ScaleTo(in Vector3 to)
        {
            var tween = AllocTween();
            tween._vectorParam1 = to;
            tween._delegate = ScaleUpdateDelegate;
            tween._startDelegate = ScaleToStartDelegate;
            return tween;
        }

        public static Tween Wait(float duration)
        {
            var tween = AllocTween();
            tween._duration = duration;
            tween._delegate = WaitUpdateDelegate;
            return tween;
        }

        public static Tween Custom (System.Func<Tween,float, bool> callback)
        {
            var tween = AllocTween();
            tween._custom = callback;
            tween._delegate = CustomUpdateDelegate;
            return tween;
        }

        public static Tween Custom(System.Func<Tween, float, bool> callback, Vector4 param1, Vector4 param2)
        {
            var tween = AllocTween();
            tween._custom = callback;
            tween._delegate = CustomUpdateDelegate;
            tween._vectorParam0 = param1;
            tween._vectorParam1 = param2;
            return tween;
        }

        public static Tween Fade(float from, float to)
        {
            var tween = AllocTween();
            tween._vectorParam0.x = from;
            tween._vectorParam1.x = to;
            tween._startDelegate = FadeStartDelegate;
            return tween;
        }

        public static Tween Color(Color from, Color to)
        {
            var tween = AllocTween();
            tween._vectorParam0 = (Vector4)from;
            tween._vectorParam1 = (Vector4)to;
            tween._startDelegate = ColorStartDelegate;
            return tween;
        }

        public static Tween Zoom (float from, float to)
        {
            var tween = AllocTween();
            tween._vectorParam0.x = from;
            tween._vectorParam1.x = to;
            tween._startDelegate = CameraStartDelegate;
            tween._delegate = ZoomUpdateDelegate;
            return tween;
        }

#if false
        public static Tween Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            var tween = AllocTween();
            tween._vectorParam0.x = volume;
            tween._vectorParam0.y = pitch;
            tween._objectParam = clip;
            tween._startDelegate = PlayStartDelegate;
            tween._delegate = WaitUpdateDelegate;
            return tween;
        }
#endif

        public static Tween Activate()
        {
            var tween = AllocTween();
            tween._startDelegate = ActivateStartDelegate;
            tween._delegate = WaitUpdateDelegate;
            tween._duration = 0.0f;
            return tween;
        }

        public static Tween Group()
        {
            var tween = AllocTween();
            tween._duration = 0f;
            return tween;
        }

        public static Tween Sequence()
        {
            var tween = AllocTween();
            tween._duration = 0f;
            tween._flags |= Flags.Sequence;
            return tween;
        }

        private void SetParent(Tween parent)
        {
            // Remove from the current parent
            if (_parent != null)
            {
                if (_prev != null)
                    _prev._next = _next;
                else
                    _parent._firstChild = _next;

                if (_next != null)
                    _next._prev = _prev;
                else
                    _parent._lastChild = _prev;

                _next = null;
                _prev = null;
                _parent = null;
            }

            // Add to the new parent
            if (parent != null)
            {
                _parent = parent;
                if (null == parent._firstChild)
                {
                    parent._firstChild = parent._lastChild = this;
                }
                else
                {
                    _prev = parent._lastChild;
                    parent._lastChild._next = this;
                    parent._lastChild = this;
                }
            }

        }

        public void Start(MonoBehaviour behavior) => Start(behavior.gameObject);

        /// <summary>
        /// Start the tween on the given node
        /// </summary>
        /// <param name="node">Node the tween should run on</param>
        public void Start(GameObject gameObject)
        {
            // If the tween has a key stop any other tweens with the same key
            if (!string.IsNullOrEmpty(_key))
                Stop(gameObject, _key);

            // Add the tween to the root tween
            _root.Child(this);
            _gameObject = gameObject;

            if (!ResolveTarget(gameObject))
                return;

            StartInternal();

            // If there is no tween manager then force the tween to stop since
            // there is non update loop that can advance time for it
            if (TweenManager.Instance == null)
                IsActive = false;
                
            if (!IsActive)
            {
                Stop(this);
                return;
            }

            // Unpause the tween system if there are tweens to run
            if (_root._firstChild != null)
                IsPaused = false; 
        }

        private bool ResolveTarget(GameObject gameObject)
        {
            // If the tween has children then resolve targets for each child
            if (_firstChild != null)
            {
                _gameObject = gameObject;
                _target = gameObject;

                Tween next;
                for (var child = _firstChild; child != null; child = next)
                {
                    next = child._next;

                    if (!child.ResolveTarget(gameObject))
                    {
                        child.SetParent(null);
                        FreeTween(child);
                    }
                }

                return (_firstChild != null);
            }

            // Resolve target
            if (!string.IsNullOrWhiteSpace(_targetName))
            {
                _gameObject = gameObject.transform.Find(_targetName)?.gameObject;
                if (_gameObject == null)
                {
                    Debug.Log($"warning: missing target '{_targetName}'");
                    FreeTween(this);
                    return false;
                }
            }
            else
            {
                _gameObject = _gameObject ?? gameObject;
                _target = _gameObject;
            }

            return true;
        }

        private bool StartInternal()
        {
            _elapsed = 0f;
            IsActive = true;
            _flags |= Flags.Started;

            // Force our duration on our children
            if (_duration > 0f)
                for (var child = _firstChild; child != null; child = child._next)
                    child.Duration(_duration);

            // Dont call start if there is a delay
            if (_delay <= 0f)
            {
                // First run the start delegate if we have one
                var result = _startDelegate?.Invoke(this, this._target as GameObject) ?? true;
                if (!result)
                    IsActive = false;                

                // If no longer active then the tween was stopped by the start method
                if (!IsActive)
                    return false;

                _onStart?.Invoke();

                UpdateInternal(0f);
            }

            return true;
        }

        private float UpdateInternal(float deltaTime)
        {
            // Delay
            if (_elapsed < _delay)
            {
                _elapsed += deltaTime;
                if (_elapsed < _delay)
                    return 0f;

                deltaTime = _elapsed - _delay;

                // Start delegate after the delay
                if (_startDelegate != null)
                {
                    if (!_startDelegate(this, _gameObject))
                        IsActive = false;

                    if (!IsActive)
                        return deltaTime;
                }

                _onStart?.Invoke();

                // Do not run delay again if looping
                if (IsLooping)
                {
                    // Handle loop count
                    if (_loopCount > 0)
                    {
                        _loopCount--;
                        if (_loopCount == 0)
                        {
                            _flags &= ~(Flags.Loop);
                            _loopCount = -1;
                        }
                    }

                    _elapsed -= _delay;
                    _delay = 0f;
                }
            }
            else
            {
                _elapsed += deltaTime;
            }

            // Group or sequence
            if (_firstChild != null)
            {
                if (IsSequence)
                    return UpdateSequence(deltaTime);

                return UpdateGroup(deltaTime);
            }

            // All other tweens
            var elapsed = _elapsed - _delay;
            var reverse = false;
            if (elapsed >= _duration)
            {
                var done = false;
                if (IsPingPong)
                {
                    elapsed -= _duration;
                    reverse = true;
                    if (elapsed >= _duration)
                    {
                        done = true;
                    }
                }
                else
                {
                    done = true;
                }

                if (done)
                {
                    if (IsLooping)
                    {
                        // Handle loop count
                        if (_loopCount > 0)
                        {
                            _loopCount--;
                            if (_loopCount == 0)
                            {
                                _flags &= ~(Flags.Loop);
                                _loopCount = -1;
                            }
                        }

                        _elapsed = elapsed % _duration;
                        reverse = false;
                        elapsed = _elapsed;
                        deltaTime = 0f;
                    }
                    else
                    {
                        deltaTime = elapsed - _duration;
                        elapsed = _duration;
                        IsActive = false;
                    }
                }
                else
                {
                    deltaTime = 0f;
                }
            }
            else
            {
                deltaTime = 0f;
            }

            float t = 0f;
            if (_duration > 0.0f)
            {
                t = elapsed / _duration;
                if (_easeDelegate != null)
                    t = _easeDelegate(t, _easeParam1, _easeParam2);

                if (reverse)
                    t = 1f - t;
            } 
            else
            {
                t = 1.0f;
            }

            // Update delegate
            _delegate(this, t);

            return deltaTime;
        }

        private float UpdateGroup(float deltaTime)
        {
            // Advance all children
            Tween next;
            var done = true;
            var remainingTime = deltaTime;
            for (var child = _firstChild; child != null; child = next)
            {
                next = child._next;

                // Start the child if not yet started.
                if (!child.IsStarted)
                {
                    if (!child.StartInternal())
                    {
                        Stop(child);
                        continue;
                    }
                }

                // This will be true on looping groups for the children who 
                // have already finished.
                if (!child.IsActive)
                {
                    if (!child.IsLooping && !IsLooping)
                        Stop(child);
                    continue;
                }

                // Advance the child
                remainingTime = Mathf.Min(remainingTime, child.UpdateInternal(deltaTime));

                done &= !child.IsActive;

                // As long as the group isnt going to loop we can just stop
                // the child as soon as its done.
                if (!child.IsActive && !IsLooping)
                    Stop(child);
            }

            if (_firstChild == null)
                IsActive = false;
            else if (done && IsLooping)
            {
                _elapsed = 0f;

                // Start the children over
                for (var child = _firstChild; child != null; child = next)
                {
                    next = child._next;
                    if (!child.StartInternal())
                        Stop(child);
                }

                // Recursively call ourself to process the rest of the time
                if (remainingTime > 0)
                    return UpdateInternal(remainingTime);
            }

            return remainingTime;
        }

        private float UpdateSequence(float deltaTime)
        {
            while (_firstChild != null)
            {
                var child = _firstChild;
                if (!child.IsActive && !child.StartInternal())
                {
                    Stop(child);
                    continue;
                }                

                deltaTime = child.UpdateInternal(deltaTime);

                if (!child.IsActive)
                {
                    if (IsLooping)
                    {
                        // Move the child to the end of the list
                        child.SetParent(null);
                        child.SetParent(this);
                    }
                    else
                    {
                        Stop(child);
                    }
                }

                if (deltaTime <= 0f)
                    break;
            }

            IsActive = _firstChild != null;

            return deltaTime;
        }

#region Start Delegates

        private static bool ColorStart(Tween tween, GameObject gameObject)
        {
            var target = gameObject.GetComponent<SpriteRenderer>();
            if (null != target)
            {
                tween._target = target;
                tween._delegate = ColorSpriteUpdateDelegate;
                return true;
            }

            // Image?
            var image = gameObject.GetComponent<UnityEngine.UI.Image>();
            if (null != image)
            {
                tween._target = image;
                tween._delegate = ColorImageUpdateDelegate;
                return true;
            }

            // Text?
            var text = gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (null != text)
            {
                tween._target = text;
                tween._delegate = ColorTextUpdateDelegate;
                return true;
            }

            Debug.Log($"warning: target has no color property to animate");
            return false;
        }

        private static bool MoveToStart(Tween tween, GameObject gameObject)
        {
            tween._target = gameObject.transform;
            tween._vectorParam0 = gameObject.transform.position;
            return true;
        }

        private static bool MoveByStart(Tween tween, GameObject gameObject)
        {
            tween._target = gameObject.transform;
            tween._vectorParam0 = gameObject.transform.position;
            tween._vectorParam1 += tween._vectorParam0;
            return true;
        }

        private static bool ScaleToStart(Tween tween, GameObject gameObject)
        {
            tween._target = gameObject.transform;
            tween._vectorParam0 = gameObject.transform.localScale;
            return true;
        }

        private static bool FadeStart(Tween tween, GameObject gameObject)
        {
            // Canvas group takes precedence.
            var canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (null != canvasGroup)
            {
                tween._target = canvasGroup;
                tween._delegate = FadeCanvasGroupUpdateDelegate;
                return true;
            }

            // Sprite?
            var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (null != spriteRenderer)
            {
                tween._target = spriteRenderer;
                tween._delegate = FadeSpriteUpdateDelegate;
                return true;
            }

            // Image?
            var image = gameObject.GetComponent<UnityEngine.UI.Image>();
            if (null != image)
            {
                tween._target = image;
                tween._delegate = FadeImageUpdateDelegate;
                return true;
            }

            // TMPro gui?
            var tmpgui = gameObject.GetComponent<TMPro.TextMeshProUGUI>();
            if (null != tmpgui)
            {
                tween._target = tmpgui;
                tween._delegate = FadeTextMeshProUIUpdateDelegate;
                return true;
            }

            // TMPro ?
            var tmpro = gameObject.GetComponent<TMPro.TextMeshPro>();
            if (null != tmpro)
            {
                tween._target = tmpro;
                tween._delegate = FadeTextMeshProUpdateDelegate;
                return true;
            }

            // Renderer
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (null != meshRenderer && meshRenderer.materials.Length == 1)
            {
                tween._target = meshRenderer;
                tween._delegate = FadeMeshRendererUpdateDelegate;
                return true;
            }

            // Renderer
            var renderer = gameObject.GetComponent<Renderer>();
            if (null != renderer && renderer.materials.Length == 1)
            {
                tween._target = renderer;
                tween._delegate = FadeRendererUpdateDelegate;
                return true;
            }

            Debug.LogWarning($"target has no alpha property to animate");

            return false;
        }

#if false
        private static bool PlayStart(Tween tween, GameObject gameObject)
        {
            var clip = (tween._objectParam as AudioClip);
            if(clip != null)
                AudioManager.Instance.Play(clip, volume, pitch);

            (tween._objectParam as AudioClip)?.Play(tween._vectorParam0.x, tween._vectorParam0.y);
            return true;
        }
#endif

        private static bool TransformStart (Tween tween, GameObject gameObject)
        {
            tween._target = gameObject.transform;
            return true;
        }

        private static bool ActivateStart (Tween tween, GameObject gameObject)
        {
            tween._target = gameObject;
            gameObject.SetActive(true);
            return true;
        }

        private static bool CameraStart (Tween tween, GameObject gameObject)
        {
            tween._target = gameObject.GetComponent<Camera>();
            return tween._target != null;
        }

        private static readonly StartDelegate TransformStartDelegate = TransformStart;
        private static readonly StartDelegate ColorStartDelegate = ColorStart;
        private static readonly StartDelegate FadeStartDelegate = FadeStart;
        private static readonly StartDelegate MoveToStartDelegate = MoveToStart;
        private static readonly StartDelegate ScaleToStartDelegate = ScaleToStart;
        private static readonly StartDelegate MoveByStartDelegate = MoveByStart;
        private static readonly StartDelegate ActivateStartDelegate = ActivateStart;
        private static readonly StartDelegate CameraStartDelegate = CameraStart;

#if false
        private static readonly StartDelegate PlayStartDelegate = PlayStart;
#endif

        #endregion

        #region Update Delegates

        private static void ShakeUpdate (Tween tween, float t)
        {
            ShakePositionUpdate(tween, t);
            ShakeRotationUpdate(tween, t);
        }

        private static void ShakePositionUpdate(Tween tween, float t)
        {            
            (tween._target as Transform).position =
                new Vector2(
                    tween._vectorParam0.x * (Mathf.PerlinNoise(tween._vectorParam1.x, t * 20f) - 0.5f) * 2.0f,
                    tween._vectorParam0.y * (Mathf.PerlinNoise(tween._vectorParam1.y, t * 20f) - 0.5f) * 2.0f
                    ) * (1f - t);
        }

        private static void ShakeRotationUpdate(Tween tween, float t)
        {
            (tween._target as Transform).localRotation =
                Quaternion.Euler(0,0,tween._vectorParam0.z * (Mathf.PerlinNoise(tween._vectorParam1.z, t * 20f) - 0.5f) * 2.0f * (1f - t));
        }

        private static void MoveUpdate(Tween tween, float t)
        {
            var pos = (Vector2)(tween._vectorParam0 * (1 - t) + tween._vectorParam1 * t);
            var rectTransform = (tween._target as RectTransform);
            if (rectTransform != null)
                rectTransform.anchoredPosition = pos;
            else
                (tween._target as Transform).localPosition = pos;
        }

        private static void MoveWorldUpdate(Tween tween, float t)
        {
            (tween._target as Transform).position = Vector4.Lerp(tween._vectorParam0, tween._vectorParam1, t);
        }

        private static void MoveToUpdate(Tween tween, float t)
        {
            (tween._target as Transform).localPosition = Vector4.Lerp(tween._vectorParam0, tween._vectorParam1, t);
        }

        private static void MoveToWorldUpdate(Tween tween, float t)
        {
            (tween._target as Transform).position = Vector4.Lerp(tween._vectorParam0, tween._vectorParam1, t);
        }

        private static void ScaleUpdate(Tween tween, float t)
        {
            var scale = (tween._vectorParam0 * (1 - t) + tween._vectorParam1 * t);
            (tween._target as Transform).localScale = new Vector3(scale.x, scale.y, scale.z);
        }

        private static void RotateUpdate(Tween tween, float t)
        {
            (tween._target as Transform).localRotation = Quaternion.Euler(Vector3.Lerp(tween.Param1, tween.Param2, t));
        }

        private static void RotateQuaternionUpdate (Tween tween, float t)
        {
            (tween._target as Transform).rotation =
                Quaternion.Slerp(
                    new Quaternion(tween._vectorParam0.x, tween._vectorParam0.y, tween._vectorParam0.z, tween._vectorParam0.w),
                    new Quaternion(tween._vectorParam1.x, tween._vectorParam1.y, tween._vectorParam1.z, tween._vectorParam1.w),
                    t
                    );
        }

        private static void RotateQuaternionLocalUpdate (Tween tween, float t)
        {
            (tween._target as Transform).localRotation =
                Quaternion.Slerp(
                    new Quaternion(tween._vectorParam0.x, tween._vectorParam0.y, tween._vectorParam0.z, tween._vectorParam0.w),
                    new Quaternion(tween._vectorParam1.x, tween._vectorParam1.y, tween._vectorParam1.z, tween._vectorParam1.w),
                    t
                    );
        }

        private static void FadeSpriteUpdate(Tween tween, float t)
        {
            var renderer = (tween._target as SpriteRenderer);
            renderer.color = new Color(
                renderer.color.r,
                renderer.color.g,
                renderer.color.b,
                tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t
                );
        }

        private static void FadeRendererUpdate (Tween tween, float t)
        {
            var renderer = (tween._target as Renderer);
            var color = renderer.material.color;
            color.a = tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t;
            renderer.material.color = color;
        }

        private static void FadeMeshRendererUpdate (Tween tween, float t)
        {
            var renderer = (tween._target as MeshRenderer);
            var color = renderer.material.color;
            color.a = tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t;
            renderer.material.color = color;
        }

        private static void FadeImageUpdate(Tween tween, float t)
        {
            var image = (tween._target as UnityEngine.UI.Image);
            image.color = new Color(
                image.color.r,
                image.color.g,
                image.color.b,
                tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t
                );
        }

        private static void FadeTextMeshProUIUpdate(Tween tween, float t)
        {
            var tmproui = (tween._target as TMPro.TextMeshProUGUI);
            tmproui.color = new Color(
                tmproui.color.r,
                tmproui.color.g,
                tmproui.color.b,
                tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t
                );
        }

        private static void FadeTextMeshProUpdate(Tween tween, float t)
        {
            var tmpro = (tween._target as TMPro.TextMeshPro);
            tmpro.color = new Color(
                tmpro.color.r,
                tmpro.color.g,
                tmpro.color.b,
                tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t
                );
        }

        private static void FadeCanvasGroupUpdate(Tween tween, float t)
        {
            var canvasgroup = (tween._target as CanvasGroup);
            canvasgroup.alpha = tween._vectorParam0.x * (1f - t) + tween._vectorParam1.x * t;
        }

        private static void ColorSpriteUpdate(Tween tween, float t)
        {
            var sprite = (tween._target as SpriteRenderer);
            var color = tween._vectorParam0 * (1f - t) + tween._vectorParam1 * t;
            sprite.color = new Color(color.x, color.y, color.z, sprite.color.a);
        }

        private static void ColorImageUpdate(Tween tween, float t)
        {
            var image = (tween._target as UnityEngine.UI.Image);
            var color = tween._vectorParam0 * (1f - t) + tween._vectorParam1 * t;
            image.color = new Color(color.x, color.y, color.z, image.color.a);
        }

        private static void ColorTextUpdate(Tween tween, float t)
        {
            var image = (tween._target as TMPro.TextMeshProUGUI);
            var color = tween._vectorParam0 * (1f - t) + tween._vectorParam1 * t;
            image.color = new Color(color.x, color.y, color.z, image.color.a);
        }

        private static void ZoomUpdate (Tween tween, float t)
        {
            (tween._target as Camera).fieldOfView = Mathf.Lerp(tween._vectorParam0.x, tween._vectorParam1.x, t);
        }

        private static void CustomUpdate(Tween tween, float t)
        {
            var result = tween._custom.Invoke(tween, t);
            if (!result)
                tween.IsActive = false;
        }
            

        private static void WaitUpdate(Tween tween, float t) { }

        private static readonly UpdateDelegate MoveUpdateDelegate = MoveUpdate;
        private static readonly UpdateDelegate MoveWorldUpdateDelegate = MoveWorldUpdate;
        private static readonly UpdateDelegate MoveToUpdateDelegate = MoveToUpdate;
        private static readonly UpdateDelegate MoveToWorldUpdateDelegate = MoveToWorldUpdate;
        private static readonly UpdateDelegate ScaleUpdateDelegate = ScaleUpdate;
        private static readonly UpdateDelegate RotateUpdateDelegate = RotateUpdate;
        private static readonly UpdateDelegate RotateQuaternionUpdateDelegate = RotateQuaternionUpdate;
        private static readonly UpdateDelegate RotateQuaternionLocalUpdateDelegate = RotateQuaternionLocalUpdate;
        private static readonly UpdateDelegate FadeSpriteUpdateDelegate = FadeSpriteUpdate;
        private static readonly UpdateDelegate FadeRendererUpdateDelegate = FadeRendererUpdate;
        private static readonly UpdateDelegate FadeImageUpdateDelegate = FadeImageUpdate;
        private static readonly UpdateDelegate FadeTextMeshProUpdateDelegate = FadeTextMeshProUpdate;
        private static readonly UpdateDelegate FadeMeshRendererUpdateDelegate = FadeMeshRendererUpdate;
        private static readonly UpdateDelegate FadeTextMeshProUIUpdateDelegate = FadeTextMeshProUIUpdate;
        private static readonly UpdateDelegate FadeCanvasGroupUpdateDelegate = FadeCanvasGroupUpdate;
        private static readonly UpdateDelegate WaitUpdateDelegate = WaitUpdate;
        private static readonly UpdateDelegate ShakeUpdateDelegate = ShakeUpdate;
        private static readonly UpdateDelegate ShakePositionUpdateDelegate = ShakePositionUpdate;
        private static readonly UpdateDelegate ShakeRotationUpdateDelegate = ShakeRotationUpdate;
        private static readonly UpdateDelegate ColorSpriteUpdateDelegate = ColorSpriteUpdate;
        private static readonly UpdateDelegate ColorImageUpdateDelegate = ColorImageUpdate;
        private static readonly UpdateDelegate ColorTextUpdateDelegate = ColorTextUpdate;
        private static readonly UpdateDelegate ZoomUpdateDelegate = ZoomUpdate;
        private static readonly UpdateDelegate CustomUpdateDelegate = CustomUpdate;

#endregion
    }
}
