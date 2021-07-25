using UnityEngine;

namespace NoZ.UI
{
    public class StylePropertyValue
    {
    }

    public class StylePropertyValue<T> : StylePropertyValue
    {
        public T value;
    }
}
