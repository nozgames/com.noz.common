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
        /*public static OpenDelegateBase Create(MethodInfo info)
        {
            var parameters = info.GetParameters();
            switch (parameters.Length)
            {
                case 0:
                    return (OpenDelegateBase)Activator.CreateInstance(typeof(OpenDelegateImpl<>).MakeGenericType(info.DeclaringType), info);

                case 1:
                    return (OpenDelegateBase)Activator.CreateInstance(typeof(OpenDelegateImpl<,>).MakeGenericType(info.DeclaringType, parameters[0].ParameterType), info);

                default:
                    throw new ArgumentException("methods parameter count not supported");
            }
        }*/
    }

    public abstract class OpenDelegate : OpenDelegateBase
    {
        public abstract void Invoke (object target);
        
        public static OpenDelegate Create(MethodInfo info)
        {
            var parameters = info.GetParameters();
            if(parameters.Length != 0)
                throw new ArgumentException("info");
            
            return (OpenDelegate)Activator.CreateInstance(typeof(OpenDelegateImpl<>).MakeGenericType(info.DeclaringType), info);
        }        
    }

    public abstract class OpenDelegate<TArg1> : OpenDelegateBase
    {
        public abstract void Invoke(object target, TArg1 arg1);

        public static OpenDelegate<TArg1> Create(MethodInfo info)
        {
            var parameters = info.GetParameters();
            if(parameters.Length != 1)
                throw new ArgumentException("info");
            
            return (OpenDelegate<TArg1>)Activator.CreateInstance(typeof(OpenDelegateImpl<,>).MakeGenericType(info.DeclaringType, parameters[0].ParameterType), info);
        }
    }

    internal class OpenDelegateImpl<TTarget> : OpenDelegate where TTarget : class
    {
        private delegate void InvokeDelegate(TTarget target);

        private readonly InvokeDelegate _invoke;

        public OpenDelegateImpl(MethodInfo methodInfo)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), methodInfo);
        }

        public void Invoke(TTarget target) => _invoke(target);

        public override void Invoke(object target) => _invoke((TTarget)target);
    }

    public class OpenDelegateImpl<TTarget,TArg1> : OpenDelegate<TArg1> where TTarget : class
    {
        private delegate void InvokeDelegate (TTarget target, TArg1 arg1);

        private readonly InvokeDelegate _invoke;

        public OpenDelegateImpl(MethodInfo methodInfo)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), methodInfo);
        }

        public void Invoke (TTarget target, TArg1 arg1) => _invoke(target, arg1);

        public override void Invoke (object target, TArg1 arg1) => _invoke((TTarget)target, arg1);
    }
}
