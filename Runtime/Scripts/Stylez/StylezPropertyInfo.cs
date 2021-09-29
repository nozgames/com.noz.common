using System;

namespace NoZ.Stylez
{
    internal class StylezPropertyInfo
    {
        public string name;
        public int nameHashId;
        public Func<string, StylezPropertyValue> thunkParse;

        /// <summary>
        /// Parse text into a property value for this property
        /// </summary>
        public StylezPropertyValue Parse(string text) => thunkParse(text);
    }

    internal class StylezPropertyInfo<T> : StylezPropertyInfo
    {
        public T defaultValue;

        public static Func<string, T> parse;

        public static StylezPropertyValue ThunkParse(string text) =>
            parse == null ? null : new StylePropertyValue<T> { value = parse.Invoke(text) };
    }
}
