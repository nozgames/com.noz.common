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

using System.Collections.Generic;
using UnityEngine;

namespace NoZ
{
    public class TweenManager : MonoBehaviour
    {
        public static TweenManager Instance = null;

        public int MaxPoolSize = 128;

        /// <summary>
        /// Pool of animations available for use
        /// </summary>
        public Queue<Tween> Pool { get; internal set; }

        public void Awake()
        {
            if(Instance != null)
            {
                Debug.LogError("Only one TweenManager can exist in a scene");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Pool = new Queue<Tween>();
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
