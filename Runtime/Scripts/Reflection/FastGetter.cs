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
using System.Linq.Expressions;
using System.Reflection;

namespace NoZ.Reflection
{
    public abstract class FastGetter
    {
        private static FastGetter Create(MemberInfo info, Type memberType)
        {
            Type genericType = typeof(FastGetter<,>).MakeGenericType(new Type[] { info.DeclaringType, memberType });
            return (FastGetter)Activator.CreateInstance(genericType, new object[] { info });
        }

        public static FastGetter Create(MemberInfo info)
        {
            switch(info.MemberType)
            {
                case MemberTypes.Field: return Create(info, (info as FieldInfo).FieldType);
                case MemberTypes.Property: return Create(info, (info as PropertyInfo).PropertyType);
                default:
                    throw new System.ArgumentException("member must be a field or property");
            }
        }
    }

    public abstract class FastGetter<V> : FastGetter
    {
        public abstract V GetValue(object target);
    }

    public class FastGetter<T, V> : FastGetter<V> where T : class
    {
        public delegate V GetValueDelegate(T target);

        private GetValueDelegate _getValue;

        public FastGetter(PropertyInfo propertyInfo)
        {
            _getValue = (GetValueDelegate)Delegate.CreateDelegate(typeof(GetValueDelegate), propertyInfo.GetMethod);
        }

        public FastGetter(FieldInfo fieldInfo)
        {
            _getValue = CreateGetFieldDelegate(fieldInfo);
        }

        public V GetValue(T target) => _getValue(target);

        public override V GetValue(object target) => _getValue((T)target);

        /// <summary>
        /// Gets a strong typed delegate to a generated method that allows you to set the field value, that is represented
        /// by the given <paramref name="fieldInfo"/>. The delegate is instance independend, means that you pass the source 
        /// of the field as a parameter to the generated method and get back the value of it's field.
        /// </summary>
        /// <typeparam name="TSource">The reflecting type. This can be an interface that is implemented by the field's declaring type
        /// or an derrived type of the field's declaring type.</typeparam>
        /// <typeparam name="TValue">The type of the field value.</typeparam>
        /// <param name="fieldInfo">Provides the metadata of the field.</param>
        /// <returns>A strong typed delegeate that can be cached to set the field's value with high performance.</returns>
        private GetValueDelegate CreateGetFieldDelegate(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                throw new ArgumentNullException("fieldInfo");

            var source = Expression.Parameter(typeof(T), "source");

            return (GetValueDelegate)Expression.Lambda(
                typeof(GetValueDelegate),
                Expression.Field(source, fieldInfo),
                source
            ).Compile();
        }
    }
}
