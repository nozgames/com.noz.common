using UnityEditor;

namespace NoZ.Style
{
    public class StyleSheetPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".uiss"))
                {
                    StyleSheet.ReloadAll();
                    return;
                }
            }
        }
    }
}
