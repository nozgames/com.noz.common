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

namespace NoZ.Reflection
{
    public class OpenDelegateBase
    {
        public static OpenDelegateBase Create(MethodInfo info)
        {
            var parameters = info.GetParameters();
            switch (parameters.Length)
            {
                case 0:
                {
                    Type genericType = typeof(OpenDelegateImpl<>).MakeGenericType(new Type[] { info.DeclaringType });
                    return (OpenDelegateBase)Activator.CreateInstance(genericType, new object[] { info });
                }

                case 1:
                {
                    Type genericType = typeof(OpenDelegateImpl<,>).MakeGenericType(new Type[] { info.DeclaringType, parameters[0].ParameterType });
                    return (OpenDelegateBase)Activator.CreateInstance(genericType, new object[] { info });
                }

                default:
                    throw new ArgumentException("methods parameter count not supported");
            }
        }
    }

    public abstract class OpenDelegate : OpenDelegateBase
    {
        public abstract void Invoke (object target);
    }

    public abstract class OpenDelegate<TArg1> : OpenDelegateBase
    {
        public abstract void Invoke(object target, TArg1 arg1);
    }

    internal class OpenDelegateImpl<TTarget> : OpenDelegate where TTarget : class
    {
        public delegate void InvokeDelegate(TTarget target);

        private InvokeDelegate _invoke;

        public OpenDelegateImpl(MethodInfo methodInfo)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), methodInfo);
        }

        public void Invoke(TTarget target) => _invoke(target);

        public override void Invoke(object target) => _invoke((TTarget)target);
    }

    public class OpenDelegateImpl<TTarget,TArg1> : OpenDelegate<TArg1> where TTarget : class
    {
        public delegate void InvokeDelegate (TTarget target, TArg1 arg1);

        private InvokeDelegate _invoke;

        public OpenDelegateImpl(MethodInfo methodInfo)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), methodInfo);
        }

        public void Invoke (TTarget target, TArg1 arg1) => _invoke(target, arg1);

        public override void Invoke (object target, TArg1 arg1) => _invoke((TTarget)target, arg1);
    }
}
