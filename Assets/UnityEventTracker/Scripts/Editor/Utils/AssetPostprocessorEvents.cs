using System;
using UnityEditor;

namespace UnityEventTracker.Utils
{
    public class AssetPostprocessorEvents : AssetPostprocessor
    {
        public static event Action<string[]> OnAssetsImported;
        public static event Action<string[]> OnAssetsDeleted;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[]                                        movedAssets,    string[] movedFromAssetPaths)
        {
            OnAssetsImported?.Invoke(importedAssets);
            OnAssetsDeleted?.Invoke(deletedAssets);
        }
    }
}
