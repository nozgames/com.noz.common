using System.IO;
using UnityEngine;
using UnityEditor;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace NoZ.Stylez
{
    [ScriptedImporter(1, "uiss")]
    public class StyleSheetImporter : ScriptedImporter
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
            var sheet = StylezSheet.Parse(text);
            if (null == sheet)
                return;

            if(sheet.hasError)
            {
                Debug.LogError($"{ctx.assetPath}({sheet.errorLine}): error: {sheet.error}", sheet);
            }

            ctx.AddObjectToAsset("Sheet", sheet);
            ctx.SetMainObject(sheet);
        }
    }
}

