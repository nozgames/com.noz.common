using System.IO;
using System.Xml.Linq;
using UnityEditor;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace NoZ.Tools
{
    /// <summary>
    /// Process visual studio projects to mantain folder structure when the project is 
    /// embedded locally.
    /// </summary>
    public class VisualStudioProjectProcessor : AssetPostprocessor
    {
        public static string OnGeneratedCSProject(string path, string content)
        {
            // Only fix ourself
            if (!path.Contains("NoZ."))
                return content;

            try
            {
                XDocument document = XDocument.Parse(content);

                XNamespace ns = document.Root.Name.Namespace;

                // get all Compile elements
                IEnumerable<XElement> compileElements = document.Root.Descendants(ns + "Compile");

                // regex to find which part of Include attribute of Compile element to use for Link element value
                // check for Editor or Runtime (recommended folders: https://docs.unity3d.com/Manual/cus-layout.html)
                Regex regex = new Regex(@"\\(Runtime|Editor)\\.*\.cs$");

                // add child Link element to each Compile element
                foreach (XElement el in compileElements)
                {
                    string fileName = el.Attribute("Include").Value;

                    Match match = regex.Match(fileName);

                    if (match.Success)
                    {
                        // substr from 1 to exclude initial slash character
                        XElement link = new XElement(ns + "Link")
                        {
                            Value = match.Value.Substring(1)
                        };

                        el.Add(link);
                    }
                }

                using (StringWriter writer = new StringWriter())
                {
                    document.Save(writer);
                    return writer.ToString();
                }
            }
            catch
            {
                return content;
            }
        }
    }
}


