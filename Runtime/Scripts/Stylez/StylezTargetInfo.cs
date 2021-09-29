using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Stylez
{
    internal class StylezTargetPropertyInfo
    {
        public StylezPropertyInfo propertyInfo;
        public Action<StylezTargetPropertyInfo, StylezSheet, StylezStyle, Component> thunkApply;

        /// <summary>
        /// Apply this property to the given component
        /// </summary>
        public void Apply(StylezSheet styleSheet, StylezStyle style, Component component) =>
            thunkApply(this, styleSheet, style, component);
    }

    internal class StylezTargetPropertyInfo<TargetType, PropertyType> : StylezTargetPropertyInfo where TargetType : Component
    {
        public Action<TargetType, PropertyType> apply;

        public static void ThunkApply(StylezTargetPropertyInfo targetPropertyInfo, StylezSheet sheet, StylezStyle style, Component component)
        {
            var targetPropertyInfoT = (targetPropertyInfo as StylezTargetPropertyInfo<TargetType, PropertyType>);
            if (null == targetPropertyInfoT)
                return;

            if (sheet.TryGetValue(style, targetPropertyInfo.propertyInfo.nameHashId, out PropertyType value))
                targetPropertyInfoT.apply(component as TargetType, value);
        }
    }

    internal class StylezTargetInfo
    {
        public Type type;
        public List<StylezTargetPropertyInfo> properties;

        public void AddProperty<TargetType, PropertyType>(StylezPropertyInfo propertyInfo, Action<TargetType, PropertyType> apply) where TargetType : Component
        {
            properties.Add(
                new StylezTargetPropertyInfo<TargetType, PropertyType>
                {
                    propertyInfo = propertyInfo,
                    thunkApply = StylezTargetPropertyInfo<TargetType, PropertyType>.ThunkApply,
                    apply = apply
                });
        }

    }
}
