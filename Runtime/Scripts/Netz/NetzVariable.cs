using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NetzVariableSerializerAttribute : Attribute
    {
        public Type type { get; set; }

        public NetzVariableSerializerAttribute(Type type)
        {
            this.type = type;
        }
    }

    public interface INetzVariableSerializer
    {
    }

    public abstract class NetzVariableSerializer<T> : INetzVariableSerializer
    {
        public abstract void Write(T val, ref DataStreamWriter writer);

        public abstract void WriteDelta(T val, T baseline, ref DataStreamWriter writer);

        public abstract T Read(ref DataStreamReader reader);
    }

    [NetzVariableSerializer(typeof(float))]
    public class NetzFloatVariableSerializer : NetzVariableSerializer<float>
    {
        public static NetworkCompressionModel _compressionModel = new NetworkCompressionModel();

        public override void Write(float value, ref DataStreamWriter writer) =>
            writer.WritePackedFloat(value, _compressionModel);

        public override void WriteDelta(float var, float baseline, ref DataStreamWriter writer) =>
            writer.WritePackedFloatDelta(var, baseline, _compressionModel);

        public override float Read (ref DataStreamReader reader) =>
            reader.ReadPackedFloat(_compressionModel);
    }

    internal static class NetzVariableSerializerFactory
    {
        private static Dictionary<Type, object> _serializers = new Dictionary<Type, object>();

        static NetzVariableSerializerFactory()
        {
            var serializerType = typeof(INetzVariableSerializer);
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach(var type in assembly.GetTypes().Where(t => serializerType.IsAssignableFrom(t)))
                {
                    var attr = type.GetCustomAttribute<NetzVariableSerializerAttribute>();
                    if (null == attr)
                        continue;

                    if(_serializers.ContainsKey(attr.type))
                    {
                        Debug.LogError($"Multiple NetzVariable serializers specified for the `{attr.type}` type.");
                        continue;
                    }

                    _serializers.Add(attr.type, Activator.CreateInstance(type));
                }
            }
        }

        public static NetzVariableSerializer<T> GetSerializer<T> () => 
            _serializers.TryGetValue(typeof(T), out var serializer) ? serializer as NetzVariableSerializer<T> : null;
    }


    public interface INetzVariable
    {
    }

    public abstract class NetzVariable : INetzVariable
    {
        /// <summary>
        /// Parent object
        /// </summary>
        internal NetzObject _parent;

        public abstract void Write(ref DataStreamWriter writer);
        public abstract void Read(ref DataStreamReader reader);
    }
        
    public class NetzVariable<T> : NetzVariable 
    {
        private static NetzVariableSerializer<T> _serializer;

        internal T _value;

        public T value
        {
            get => _value;
            set
            {
                // Check for the value not changing
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;

                _value = value;
                _parent.MarkChanged();
            }
        }

        static NetzVariable()
        {
            _serializer = NetzVariableSerializerFactory.GetSerializer<T>();
        }

        public override void Write (ref DataStreamWriter writer)
        {
            _serializer.Write(_value, ref writer);
        }

        public void WriteDelta(NetzVariable<T> baseline, ref DataStreamWriter writer)
        {
            _serializer.WriteDelta(_value, baseline._value, ref writer);
        }

        public override void Read(ref DataStreamReader reader)
        {
            _value = _serializer.Read(ref reader);
        }
    }
}
