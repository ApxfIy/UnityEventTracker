using System;
using UnityEditor;

namespace UnityEventTracker.Utils
{
    public sealed class AssetModificationEvents : AssetModificationProcessor
    {
        public static event Action<string>   OnBeforeDelete;
        public static event Action<string>   OnBeforeCreate;
        public static event Action<string[]> OnBeforeSave;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            OnBeforeSave?.Invoke(paths);
            return paths;
        }

        private static void OnWillCreateAsset(string path)
        {
            OnBeforeCreate?.Invoke(path);
        }

        private static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            OnBeforeDelete?.Invoke(path);
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
