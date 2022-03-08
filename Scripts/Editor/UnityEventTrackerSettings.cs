using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEventTracker
{
    [Serializable]
    internal class UnityEventTrackerSettings : ScriptableObject
    {
        [Header("Tracking settings")]
        [SerializeField]
        [Tooltip("If set to false UnityEventTracker will not be tracking changes in scripts/assets." +
                 "If any scripts/assets has changed when UnityEventTracker was disabled you need to " +
                 "scan whole project via Tools/UnityEventTracker/Scan Project command")]
        private ObservableProperty<bool> _isTrackingEnabled = true;

        [SerializeField]
        [Tooltip("If set to true UnityEventHelperWindow will be opened automatically when invalid call is detected")]
        private bool _openWindowAutomatically = true;

        [SerializeField]
        [Tooltip("When project is scanned this folders will be skipped. Path is relative to Assets folder")]
        private string[] _foldersToIgnore = new[]
        {
            "Plugins",
            "Samples"
        };

        [Space(10f)]
        [Header("Highlighting settings")]
        [SerializeField]
        [Tooltip("If set to false UnityEventHighlighter will not be tracking changes in scripts/assets." +
                 "If any scripts/assets has changed when UnityEventController was disabled you need to " +
                 "scan whole project via Tools/UnityEventTracker/Scan Project command")]
        private ObservableProperty<bool> _isHighlightingEnabled = true;

        [SerializeField] private ObservableProperty<Color> _validCallColor   = new Color(0, 1, 0, 0.25f);
        [SerializeField] private ObservableProperty<Color> _invalidCallColor = new Color(1, 0, 0, 0.25f);

        public ObservableProperty<bool>  IsTrackingEnabled       => _isTrackingEnabled;
        public ObservableProperty<bool>  IsHighlightingEnabled   => _isHighlightingEnabled;
        public bool                      OpenWindowAutomatically => _openWindowAutomatically;
        public ObservableProperty<Color> ValidCallColor          => _validCallColor;
        public ObservableProperty<Color> InvalidCallColor        => _invalidCallColor;

        public bool InInitialScanComplete
        {
            get => PlayerPrefs.GetInt("UnityEventTrackerSettings_InInitialScanComplete", 0) == 1;
            set => PlayerPrefs.SetInt("UnityEventTrackerSettings_InInitialScanComplete", value ? 1 : 0);
        }

        [MenuItem("Tools/UnityEventTracker/Show Settings")]
        public static void ShowUnityEventHelperSettings()
        {
            var instance = Instance();

            Selection.activeObject = instance;
            EditorGUIUtility.PingObject(instance);
        }

        public bool ShouldSkipFolder(string relativePath)
        {
            return _foldersToIgnore.Any(f => relativePath.StartsWith($"Assets/{f}"));
        }

        private static UnityEventTrackerSettings _instance;

        public static UnityEventTrackerSettings Instance()
        {
            if (_instance != null)
                return _instance;

            var guids = AssetDatabase.FindAssets("t:" + nameof(UnityEventTrackerSettings));

            _instance = guids.Length == 0
                ? CreateDefaultSettings()
                : AssetDatabase.LoadAssetAtPath<UnityEventTrackerSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return _instance;
        }

        private static UnityEventTrackerSettings CreateDefaultSettings()
        {
            var instance = CreateInstance<UnityEventTrackerSettings>();

            var path = CreateRootDirectory();

            var filePath = Path.Combine(path, "UnityEventHelperSettings.asset");
            AssetDatabase.CreateAsset(instance, filePath);
            AssetDatabase.SaveAssets();
            return instance;
        }

        private static string CreateRootDirectory()
        {
            var rootPath = UnityEventTracker.RootPath;

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            return rootPath.Substring(rootPath.IndexOf("Assets", StringComparison.InvariantCulture));
        }
    }
}