using UnityEngine;

namespace NoZ.Stylez
{
    public class StylezPropertyValue
    {
    }

    public class StylePropertyValue<T> : StylezPropertyValue
    {
        public T value;
    }
}
