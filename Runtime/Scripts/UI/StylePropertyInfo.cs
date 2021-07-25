using System;

namespace NoZ.UI
{
    internal class StylePropertyInfo
    {
        public string name;
        public int nameHashId;
        public Func<string, StylePropertyValue> thunkParse;

        /// <summary>
        /// Parse text into a property value for this property
        /// </summary>
        public StylePropertyValue Parse(string text) => thunkParse(text);
    }

    internal class StylePropertyInfo<T> : StylePropertyInfo
    {
        public T defaultValue;

        public static Func<string, T> parse;

        public static StylePropertyValue ThunkParse(string text) =>
            parse == null ? null : new StylePropertyValue<T> { value = parse.Invoke(text) };
    }
}
