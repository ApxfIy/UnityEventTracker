using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEventTracker.Utils;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.EditorWindow
{
    internal abstract class BaseTab
    {
        internal          Rect   Rect { get; set; }
        internal abstract string Name { get; }

        private readonly HashSet<int> _activeAssetsIndices = new HashSet<int>();

        internal abstract void Draw();

        internal virtual void OnEnable()
        {
            PrefabStage.prefabStageOpened   += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing  += OnPrefabStageChanged;
            EditorSceneManager.sceneOpened  += OnSceneOpened;
            UnityEventTracker.OnDataChanged += OnDataChanged;
        }

        internal virtual void OnDisable()
        {
            PrefabStage.prefabStageOpened   -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing  -= OnPrefabStageChanged;
            EditorSceneManager.sceneOpened  -= OnSceneOpened;
            UnityEventTracker.OnDataChanged -= OnDataChanged;
        }

        protected virtual void OnDataChanged()
        {
        }

        protected void UpdateActiveAssetAndRelatedData()
        {
            DetermineActiveAssets();
            UpdateAddressesMapping();
        }

        private Vector2 _assetsScrollPos;

        protected void DrawAssets(float width)
        {
            _assetsScrollPos = EditorGUILayout.BeginScrollView(_assetsScrollPos);

            for (var i = 0; i < _currentAssets.Length; i++)
            {
                var assetInfo = _currentAssets[i];

                if (_activeAssetsIndices.Contains(i))
                    DrawActiveAsset(assetInfo, width);
                else
                    DrawNonActiveAsset(assetInfo, width);
            }

            EditorGUILayout.EndScrollView();
        }

        private AssetInfo[]                         _currentAssets = new AssetInfo[0];
        private Dictionary<AssetInfo, GameObject[]> _targets       = new Dictionary<AssetInfo, GameObject[]>();

        protected void UpdateCurrentAssetsArray(IEnumerable<Address> addresses)
        {
            _currentAssets = addresses.GroupBy(a => a.AssetGuid).Select(p =>
            {
                var guid = p.Key;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj  = AssetDatabase.LoadAssetAtPath<Object>(path);
                var icon = AssetPreview.GetMiniThumbnail(obj);

                var shortName  = Path.GetFileName(path);
                var guiContent = new GUIContent(shortName, icon, path);
                var asset      = Asset.FromRelativePath(path).GetValueUnsafe();
                var assetInfo  = new AssetInfo(asset, guiContent, p.Select(d => d).ToArray());
                return assetInfo;
            }).ToArray();
        }

        private void OnPrefabStageChanged(PrefabStage stage)
        {
            UpdateActiveAssetAndRelatedData();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            UpdateActiveAssetAndRelatedData();
        }

        private void UpdateAddressesMapping()
        {
            if (!HasActiveAssets())
            {
                _targets = new Dictionary<AssetInfo, GameObject[]>();
                return;
            }

            _targets.Clear();

            var assetInfos = GetActiveAssetInfos();

            foreach (var assetInfo in assetInfos)
            {
                var          asset = assetInfo.Asset;
                GameObject[] currentObjects;

                switch (asset.AssetType)
                {
                    case AssetType.Scene:
                        currentObjects = SceneManager.GetSceneByPath(asset.RelativePath).GetRootGameObjects()
                                                     .SelectMany(GameObjectUtils.TraverseRoot).ToArray();
                        break;
                    case AssetType.Prefab:
                    {
                        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                        currentObjects = GameObjectUtils.TraverseRoot(prefabStage.prefabContentsRoot).ToArray();
                        break;
                    }
                    default:
                        _targets.Add(assetInfo, new GameObject[0]);
                        continue;
                }

                var assetAddresses = assetInfo.Addresses;
                var targets        = new GameObject[assetAddresses.Length];

                for (var i = 0; i < assetAddresses.Length; i++)
                {
                    var address      = assetAddresses[i];
                    var gameObjectId = ulong.Parse(address.GameObjectId);
                    var target = currentObjects.FirstOrDefault(gameObject =>
                        GlobalObjectId.GetGlobalObjectIdSlow(gameObject.GetInstanceID())
                                      .targetObjectId == gameObjectId);

                    targets[i] = target;
                }

                _targets.Add(assetInfo, targets);
            }
        }

        private void DetermineActiveAssets()
        {
            _activeAssetsIndices.Clear();

            for (var i = 0; i < _currentAssets.Length; i++)
            {
                var asset = _currentAssets[i].Asset;

                if (!asset.IsLoaded()) continue;

                _activeAssetsIndices.Add(i);
            }
        }

        private bool HasActiveAssets()
        {
            return _activeAssetsIndices.Count > 0;
        }

        private IEnumerable<AssetInfo> GetActiveAssetInfos()
        {
            return _currentAssets.Where((t, i) => _activeAssetsIndices.Contains(i));
        }

        private void DrawActiveAsset(AssetInfo assetInfo, float width)
        {
            var fileContent = assetInfo.GuiContent;

            var defaultColor = GUI.color;
            GUI.color = Color.green;

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.color = defaultColor;

                using (new ColorScope(
                    ColorScope.ColorOrDefault(() => assetInfo.Asset.IsDirty(), new Color(0.91f, 0.44f, 0.26f, 1),
                        ColorType.All),
                    ColorType.All))
                {
                    var content = new GUIContent(fileContent);

                    if (assetInfo.Asset.IsDirty())
                        content.text += " (Modified)";

                    GUILayout.Button(content, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2),
                        GUILayout.Width(width));
                }

                var targets = _targets[assetInfo];

                foreach (var target in targets)
                {
                    // Was deleted
                    if (target == null)
                    {
                        using (new ColorScope(Color.red, ColorType.Background))
                            GUILayout.Button("Deleted");
                        
                        continue;
                    }

                    var isClicked = GUILayout.Button(target.name);

                    if (!isClicked) continue;

                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                }
            }
        }

        private static void DrawNonActiveAsset(AssetInfo assetInfo, float width)
        {
            var asset       = assetInfo.Asset;
            var fileContent = assetInfo.GuiContent;

            var isClicked = GUILayout.Button(fileContent, 
                GUILayout.Height(EditorGUIUtility.singleLineHeight * 2), GUILayout.Width(width));

            if (!isClicked) return;

            switch (asset.AssetType)
            {
                case AssetType.Scene:
                    EditorSceneManager.OpenScene(asset.RelativePath);
                    break;
                case AssetType.Prefab:
                    PrefabStageUtility.OpenPrefab(asset.RelativePath);
                    break;
                default:
                {
                    var target = AssetDatabase.LoadAssetAtPath<Object>(asset.RelativePath);
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                    break;
                }
            }
        }
        
        private class AssetInfo
        {
            public Asset      Asset      { get; }
            public GUIContent GuiContent { get; }
            public Address[]  Addresses  { get; }

            public AssetInfo(Asset asset, GUIContent guiContent, Address[] addresses)
            {
                Asset      = asset;
                GuiContent = guiContent;
                Addresses  = addresses;
            }
        }
    }
}