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
    public partial class Tween
    {
        public Tween EaseInBack(float amplitude = 1f) => Easing(EaseType.EaseInBack, amplitude, 0f);
        public Tween EaseOutBack(float amplitude = 1f) => Easing(EaseType.EaseOutBack, amplitude, 0f);
        public Tween EaseInOutBack(float amplitude = 1f) => Easing(EaseType.EaseInOutBack, amplitude, 0f);

        public Tween EaseInElastic(int oscillations, float springiness) => Easing(EaseType.EaseInElastic, oscillations, springiness);
        public Tween EaseInElastic() => Easing(EaseType.EaseInElastic);
        public Tween EaseInOutElastic(int oscillations, float springiness) => Easing(EaseType.EaseInOutElastic, oscillations, springiness);
        public Tween EaseInOutElastic() => Easing(EaseType.EaseInOutElastic);
        public Tween EaseOutElastic(int oscillations, float springiness) => Easing(EaseType.EaseOutElastic, oscillations, springiness);
        public Tween EaseOutElastic() => Easing(EaseType.EaseOutElastic);

        public Tween EaseInCubic() => Easing(EaseType.EaseInCubic);
        public Tween EaseOutCubic() => Easing(EaseType.EaseOutCubic);
        public Tween EaseInOutCubic() => Easing(EaseType.EaseInOutCubic);

        public Tween EaseInBounce(int oscillations, float springiness) => Easing(EaseType.EaseInBounce, oscillations, springiness);
        public Tween EaseInBounce() => Easing(EaseType.EaseInBounce);
        public Tween EaseInOutBounce(int oscillations, float springiness) => Easing(EaseType.EaseInOutBounce, oscillations, springiness);
        public Tween EaseInOutBounce() => Easing(EaseType.EaseInOutBounce);
        public Tween EaseOutBounce(int oscillations, float springiness) => Easing(EaseType.EaseOutBounce, oscillations, springiness);
        public Tween EaseOutBounce() => Easing(EaseType.EaseOutBounce);

        public Tween Easing(EaseType easeType)
        {
            _easeDelegate = _easingDelegates[(int)easeType];

            switch (easeType)
            {
                case EaseType.EaseInBounce:
                case EaseType.EaseInOutBounce:
                case EaseType.EaseOutBounce:
                    _easeParam1 = 3f;
                    _easeParam2 = 2f;
                    break;

                case EaseType.EaseInElastic:
                case EaseType.EaseOutElastic:
                case EaseType.EaseInOutElastic:
                    _easeParam1 = 3f;
                    _easeParam2 = 3f;
                    break;

                default:
                    _easeParam1 = 0f;
                    _easeParam2 = 0f;
                    break;
            }

            return this;
        }

        public Tween Easing(EaseType easeType, float param1)
        {
            Easing(easeType);
            _easeParam1 = param1;
            return this;
        }

        public Tween Easing(EaseType easeType, float param1, float param2)
        {
            _easeDelegate = _easingDelegates[(int)easeType];
            _easeParam1 = param1;
            _easeParam2 = param2;
            return this;
        }

        private static float EaseOut(float t, float p1, float p2, EaseType easeType) => 1f - _easingDelegates[(int)easeType](1f - t, p1, p2);
        private static float EaseInOut(float t, float p1, float p2, EaseType easeType)
        {
            var easeIn = _easingDelegates[(int)easeType];
            return (t < 0.5f) ?
                easeIn(t * 2f, p1, p2) * 0.5f :
                (1f - easeIn((1f - t) * 2f, p1, p2)) * 0.5f + 0.5f;
        }

        private static float EaseInCubic(float t, float p1, float p2) => t * t * t;
        private static float EaseOutCubic(float t, float p1, float p2) => EaseOut(t, p1, p2, EaseType.EaseInCubic);
        private static float EaseInOutCubic(float t, float p1, float p2) => EaseInOut(t, p1, p2, EaseType.EaseInCubic);

        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseOutCubic(float t) => EaseOut(t, 0.0f, 0.0f, EaseType.EaseInCubic);
        public static float EaseInOutCubic(float t) => EaseInOut(t, 0.0f, 0.0f, EaseType.EaseInCubic);

        private static float EaseInBack(float t, float p1, float p2)
        {
            return Mathf.Pow(t, 3f) - t * Mathf.Max(0f, p1) * Mathf.Sin(Mathf.PI * t);
        }
        private static float EaseOutBack(float t, float p1, float p2) => EaseOut(t, p1, p2, EaseType.EaseInBack);
        private static float EaseInOutBack(float t, float p1, float p2) => EaseInOut(t, p1, p2, EaseType.EaseInBack);

        private static float EaseInBounce(float t, float p1, float p2)
        {
            var Bounces = p1;
            var Bounciness = p2;

            var pow = Mathf.Pow(Bounciness, Bounces);
            var invBounciness = 1f - Bounciness;

            var sum_units = (1f - pow) / invBounciness + pow * 0.5f;
            var unit_at_t = t * sum_units;

            var bounce_at_t = Mathf.Log(-unit_at_t * invBounciness + 1f, Bounciness);
            var start = Mathf.Floor(bounce_at_t);
            var end = start + 1f;

            var div = 1f / (invBounciness * sum_units);
            var start_time = (1f - Mathf.Pow(Bounciness, start)) * div;
            var end_time = (1f - Mathf.Pow(Bounciness, end)) * div;

            var mid_time = (start_time + end_time) * 0.5f;
            var peak_time = t - mid_time;
            var radius = mid_time - start_time;
            var amplitude = Mathf.Pow(1f / Bounciness, Bounces - start);

            return (-amplitude / (radius * radius)) * (peak_time - radius) * (peak_time + radius);
        }
        private static float EaseOutBounce(float t, float p1, float p2) => EaseOut(t, p1, p2, EaseType.EaseInBounce);
        private static float EaseInOutBounce(float t, float p1, float p2) => EaseInOut(t, p1, p2, EaseType.EaseInBounce);

        private static float EaseInElastic(float t, float oscillations, float springiness)
        {
            oscillations = Mathf.Max(0, (int)oscillations);
            springiness = Mathf.Max(0f, springiness);

            float expo;
            if (springiness == 0f)
                expo = t;
            else
                expo = (Mathf.Exp(springiness * t) - 1f) / (Mathf.Exp(springiness) - 1f);

            return expo * (Mathf.Sin((Mathf.PI * 2f * oscillations + Mathf.PI * 0.5f) * t));
        }
        private static float EaseOutElastic(float t, float p1, float p2) => EaseOut(t, p1, p2, EaseType.EaseInElastic);
        private static float EaseInOutElastic(float t, float p1, float p2) => EaseInOut(t, p1, p2, EaseType.EaseInElastic);
    }
}
