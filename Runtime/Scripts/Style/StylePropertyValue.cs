using UnityEngine;

namespace NoZ.Style
{
    public class StylePropertyValue
    {
    }

    public class StylePropertyValue<T> : StylePropertyValue
    {
        public T value;
    }
}
