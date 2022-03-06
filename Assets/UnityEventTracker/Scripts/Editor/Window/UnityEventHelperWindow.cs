using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEventTracker.EditorWindow
{
    public class UnityEventHelperWindow : UnityEditor.EditorWindow
    {
        private const string Title = "EventsTracker";

        private static readonly BaseTab[] Tabs =
        {
            new ValidCallsTab(),
            new InvalidTargetsTab(),
            new InvalidMethodsTab(),
            new InvalidArgumentsTab(),
        };

        private int _currentTabIndex = 0;

        [MenuItem("Tools/UnityEventTracker/Open Window", false, 0)]
        public static void Open()
        {
            if (HasOpenInstances<UnityEventHelperWindow>()) return;

            //var isSaved =
            //    AssetUtils.AskUserToSaveModifiedAssets(2,
            //        "All assets and scene must be saved, before using this window");

            //if (!isSaved) return;

            // TODO check if Application is playing
            var window = GetWindow<UnityEventHelperWindow>(false, Title);
            window.Show();
        }

        public static void ShutDown()
        {
            if (!HasOpenInstances<UnityEventHelperWindow>()) return;

            var window = GetWindow<UnityEventHelperWindow>();
            window.Close();
        }

        private void OnEnable()
        {
            Tabs[_currentTabIndex].OnEnable();
        }

        private double _updateInterval = 0.5d;
        private double _nextUpdateTime;

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextUpdateTime) return;

            Repaint();
            _nextUpdateTime = EditorApplication.timeSinceStartup + _updateInterval;
        }

        private void OnGUI()
        {
            DrawCurrentTab();
        }

        private void DrawCurrentTab()
        {
            var newTabIndex = GUILayout.Toolbar(_currentTabIndex, Tabs.Select(t => t.Name).ToArray());

            if (newTabIndex != _currentTabIndex)
            {
                Tabs[_currentTabIndex].OnDisable();
                _currentTabIndex = newTabIndex;
                Tabs[_currentTabIndex].OnEnable();
            }

            if (Event.current.type == EventType.Repaint)
            {
                var availableRect = CalculateAvailableRect();
                Tabs[_currentTabIndex].Rect = availableRect;
            }

            Tabs[_currentTabIndex].Draw();
        }

        private Rect CalculateAvailableRect()
        {
            var toolbarRect = GUILayoutUtility.GetLastRect();
            var wholeRect   = position;
            var yOffset     = toolbarRect.y - wholeRect.y;

            return new Rect(
                toolbarRect.x,
                toolbarRect.y + toolbarRect.height,
                toolbarRect.width,
                wholeRect.height - toolbarRect.height - yOffset);
        }
    }
}