using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Style
{
    internal class StyleTargetPropertyInfo
    {
        public StylePropertyInfo propertyInfo;
        public Action<StyleTargetPropertyInfo, StyleSheet, Style, Component> thunkApply;

        /// <summary>
        /// Apply this property to the given component
        /// </summary>
        public void Apply(StyleSheet styleSheet, Style style, Component component) =>
            thunkApply(this, styleSheet, style, component);
    }

    internal class StyleTargetPropertyInfo<TargetType, PropertyType> : StyleTargetPropertyInfo where TargetType : Component
    {
        public Action<TargetType, PropertyType> apply;

        public static void ThunkApply(StyleTargetPropertyInfo targetPropertyInfo, StyleSheet sheet, Style style, Component component)
        {
            var targetPropertyInfoT = (targetPropertyInfo as StyleTargetPropertyInfo<TargetType, PropertyType>);
            if (null == targetPropertyInfoT)
                return;

            targetPropertyInfoT.apply(
                component as TargetType,  
                sheet.GetValue(
                    style, 
                    targetPropertyInfo.propertyInfo.nameHashId, 
                    (targetPropertyInfoT.propertyInfo as StylePropertyInfo<PropertyType>).defaultValue));
        }
    }

    internal class StyleTargetInfo
    {
        public Type type;
        public List<StyleTargetPropertyInfo> properties;

        public void AddProperty<TargetType, PropertyType>(StylePropertyInfo propertyInfo, Action<TargetType, PropertyType> apply) where TargetType : Component
        {
            properties.Add(
                new StyleTargetPropertyInfo<TargetType, PropertyType>
                {
                    propertyInfo = propertyInfo,
                    thunkApply = StyleTargetPropertyInfo<TargetType, PropertyType>.ThunkApply,
                    apply = apply
                });
        }

    }
}
