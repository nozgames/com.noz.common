using UnityEditor;

namespace NoZ.Stylez
{
    public class StyleSheetPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".uiss"))
                {
                    StylezSheet.ReloadAll();
                    return;
                }
            }
        }
    }
}
