using System;
using UnityEngine;

namespace NoZ.UI
{
    internal class StylePropertyInfo
    {
        public string name;
        public int nameHashId;
        public Action<StylePropertyInfo, StyleSheet, Style, Component> thunkApply;
        public Func<string, StylePropertyValue> thunkParse;

        /// <summary>
        /// Apply this property to the given component
        /// </summary>
        public void Apply(StyleSheet styleSheet, Style style, Component component) =>
            thunkApply(this, styleSheet, style, component);

        /// <summary>
        /// Parse text into a property value for this property
        /// </summary>
        public StylePropertyValue Parse(string text) => thunkParse(text);
    }

    internal class StylePropertyInfo<T> : StylePropertyInfo
    {
        public Action<Component, T> apply;
        public T defaultValue;

        public static Func<string, T> parse;

        public static void ThunkApply(StylePropertyInfo propertyInfo, StyleSheet sheet, Style style, Component component)
        {
            var propertyInfoT = (propertyInfo as StylePropertyInfo<T>);
            if (null == propertyInfoT)
                return;

            propertyInfoT.apply(component, sheet.GetValue<T>(style, propertyInfo.nameHashId, propertyInfoT.defaultValue));
        }

        public static StylePropertyValue ThunkParse(string text) =>
            parse == null ? null : new StylePropertyValue<T> { value = parse.Invoke(text) };
    }
}
