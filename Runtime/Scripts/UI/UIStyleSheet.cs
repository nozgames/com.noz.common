using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace NoZ.UI
{
    public class UIStyleSheet : ScriptableObject
    {
        [Serializable]
        public class Selector
        {
            public string value;
        }

        [Serializable]
        public class Property
        {
            public string name;
            public string value;
        }

        [Serializable]
        public class Style
        {
            public Selector[] selectors;
            public Property[] properties;
        }

        public Style[] _styles = null;

        public static UIStyleSheet Create (Style[] styles)
        {
            var sheet = CreateInstance<UIStyleSheet>();
            sheet._styles = styles;
            return sheet;
        }

        public Color GetColor (UIStyle style)
        {
            if (_styles == null)
                return Color.white;

            var search = $"button{(style.isSelected ? ":selected" : (((style.isPressed) ? ":pressed": (style.isHover ? ":hover" : ""))))}";
            var selected = _styles
                .Where(s => string.Compare(s.selectors[0].value, search, true) == 0)
                .FirstOrDefault();

            var property = selected.properties
                .Where(p => string.Compare(p.name, "color", true) == 0)
                .FirstOrDefault();

            if (ColorUtility.TryParseHtmlString(property.value, out var color))
                return color;

            return Color.white;
        }

        public static event Action onReload;

        public static void ReloadAll ()
        {
            onReload?.Invoke();
        }
    }
}

