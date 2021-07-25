using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace NoZ.UI
{
    [ScriptedImporter(1, "uiss")]
    public class UIStyleSheetImporter : ScriptedImporter
    {
        private static string GetSelectedPathOrFallback()
        {
            string path = "Assets";

            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }

        [MenuItem("Assets/Create/NoZ/UI/StyleSheet")]
        private static void CreateEmpty()
        {
            var filename = Path.Combine(
                Application.dataPath,
                AssetDatabase.GenerateUniqueAssetPath($"{GetSelectedPathOrFallback()}/New StyleSheet.uiss").Substring(7));

            File.WriteAllText(filename, "");
            AssetDatabase.Refresh();
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var sheet = StyleSheet.Parse(text);
            if (null == sheet)
                return;

            ctx.AddObjectToAsset("Sheet", sheet);
            ctx.SetMainObject(sheet);
        }
    }
}

