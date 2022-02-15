using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.UIElements.VisualElement;

namespace NoZ
{
    public static class VisualElementExtensions
    {
        public static TElement Add<TElement> (this VisualElement parent, string name=null, string className=null) where TElement : VisualElement, new()
        {
            var element = new TElement ();
            if (name != null)
                element.name = name;

            if(className != null)
                element.AddToClassList(className);

            parent.Add(element);
            return element;
        }

        public static TElement Add<TElement>(this Hierarchy parent, string name = null, string className = null) where TElement : VisualElement, new()
        {
            var element = new TElement();
            if (name != null)
                element.name = name;

            if (className != null)
                element.AddToClassList(className);

            parent.Add(element);
            return element;
        }

        public static TElement Text<TElement> (this TElement element, string text) where TElement : VisualElement
        {
            if (element is TextElement textElement)
                textElement.text = text;
            else if (element is RadioButton radioButton)
                radioButton.text = text;
            
            return element;
        }

        public static TElement AddClass<TElement> (this TElement element, string className) where TElement : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
