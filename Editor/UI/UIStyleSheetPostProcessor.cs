using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NoZ.UI
{
    public class UIStyleSheetPostProcessor : AssetPostprocessor
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
