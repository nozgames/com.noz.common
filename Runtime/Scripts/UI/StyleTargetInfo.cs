using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.UI
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

            if (null == (targetPropertyInfoT.propertyInfo as StylePropertyInfo<PropertyType>))
                Debug.Log("#1");

            if (null == targetPropertyInfo.propertyInfo)
                Debug.Log("#2");

            if (null == sheet)
                Debug.Log("#3");

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
    }
}
