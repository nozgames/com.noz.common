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
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Tweenz
{
    internal class TweenManager : Singleton<TweenManager>
    {
        private const int MaxTweens = 1024;

        private Tween[] _tweens = new Tween[1024];

        private List<Tween> _pool = new List<Tween>(MaxTweens);

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // Preallocate the maximum number of tweens for simplicity
            // TODO: we could make this more on demand later
            for (int i = 0; i < MaxTweens; i++)
            {
                _tweens[i] = new Tween();
                _tweens[i]._id._index = (uint)i;
                _tweens[i]._id._iteration = 1;
                _tweens[i]._flags |= Tween.Flags.Free;
                _pool.Add(_tweens[i]);
            }
        }

        internal Tween AllocTween()
        {
            if (_pool.Count <= 0)
                throw new InvalidOperationException("Maximum Tweenz count exceeded.");

            var tween = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);

            if (!tween.IsFree)
                throw new InvalidOperationException("Tween in free list that was not free");

            tween._id._iteration++;
            return tween;
        }

        internal void FreeTween (Tween tween)
        {
            if (tween.IsFree)
                return;

            _pool.Add(tween);
        }

        public Tween GetTween (TweenzId id)
        {
            var tween = _tweens[id._index];
            if (null == tween || id._iteration != tween._id._iteration || tween.IsFree)
                return null;

            return tween;
        }

        public void Update()
        {
            Tween.Update(TweenUpdateMode.Update);
        }

        public void FixedUpdate()
        {
            Tween.Update(TweenUpdateMode.FixedUpdate);
        }
    }
}
