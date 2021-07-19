using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.UI
{
    public static class UIStyleSheetParser
    {
        public static UIStyleSheet Parse(Stream stream)
        {
            using var streamReader = new StreamReader(stream);
            return Parse(streamReader.ReadToEnd());
        }

        private static Regex ParseRegex = new Regex(@"((\.\w[\w\d_\-]*|\#\w[\w\d_\-\:]*)+|\w[\w\d_\-\:]*|\{|\}|\:|\d|\;|\#[\dABCDEF]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public static UIStyleSheet Parse(string text)
        {
            var tokens = ParseRegex.Matches(text);
            if (tokens.Count == 0)
                return null;

            try
            {
                var styles = new List<UIStyleSheet.Style>();
                for (var tokenIndex = 0; tokenIndex < tokens.Count;)
                {
                    var style = new UIStyleSheet.Style();
                    ParseSelectors(text, tokens, ref tokenIndex, style);
                    ParseDefinition(text, tokens, ref tokenIndex, style);
                    styles.Add(style);
                }

                return UIStyleSheet.Create(styles.ToArray());

            } 
            catch (System.IndexOutOfRangeException)
            {
                Debug.LogError($"error: {GetLineNumber(text, text.Length - 1)}: unexpected EOF ");
            } catch (System.Exception e)
            {
                Debug.LogError($"error: {e.Message} ");
            }

            return null;
        }

        private static void ParseSelectors(string text, MatchCollection tokens, ref int tokenIndex, UIStyleSheet.Style style)
        {
            var selectors = new List<UIStyleSheet.Selector>();
            while (tokens[tokenIndex].Value != "{")
            {
                var selector = tokens[tokenIndex++].Value;
                if (selector == "}" || selector == ":" || selector == ";")
                    throw new System.FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: unexpected token \"{selector}\"");

                selectors.Add(new UIStyleSheet.Selector { value = selector });
            }

            style.selectors = selectors.ToArray();
        }

        private static void ParseDefinition(string text, MatchCollection tokens, ref int tokenIndex, UIStyleSheet.Style style)
        {
            if (tokens[tokenIndex++].Value != "{")
                throw new System.FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: missing \"{{\"");

            // Read until the end brace
            var properties = new List<UIStyleSheet.Property>();
            while (tokens[tokenIndex].Value != "}")
                properties.Add(ParseProperty(text, tokens, ref tokenIndex));

            style.properties = properties.ToArray();

            tokenIndex++;
        }

        private static UIStyleSheet.Property ParseProperty(string text, MatchCollection tokens, ref int tokenIndex)
        {
            var name = tokens[tokenIndex++].Value;

            if (tokens[tokenIndex++].Value != ":")
                throw new System.FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: Missing \":\"");

            var value = tokens[tokenIndex++].Value;

            if (tokens[tokenIndex++].Value != ";")
                throw new System.FormatException($"{GetLineNumber(text, tokens[tokenIndex - 1])}: Missing \";\"");

            return new UIStyleSheet.Property { name = name, value = value};
        }

        private static int GetLineNumber(string text, Match match) => GetLineNumber(text, match.Index);

        private static int GetLineNumber(string text, int index)
        {
            var lineNumber = 1;
            for (var i = index; i >= 0; i--)
                lineNumber += text[i] == '\n' ? 1 : 0;

            return lineNumber;
        }
    }
}
