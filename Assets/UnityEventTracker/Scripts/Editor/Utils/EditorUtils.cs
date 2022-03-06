using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityEventTracker.Utils
{
    public static class EditorUtils
    {
        // Source https://gamedev.stackexchange.com/questions/137523/unity-json-utility-does-not-serialize-datetime
        [Serializable]
        private struct JsonDateTime
        {
            public long Value;

            public static implicit operator DateTime(JsonDateTime jdt)
            {
                return DateTime.FromBinary(jdt.Value);
            }

            public static implicit operator JsonDateTime(DateTime dt)
            {
                var jdt = new JsonDateTime {Value = dt.ToBinary()};
                return jdt;
            }
        }

        public static bool IsEditorFocused => UnityEditorInternal.InternalEditorUtility.isApplicationActive;
        public static event Action<bool> OnEditorFocusStateChanged;

        public static DateTime LastUnfocusedEventTime
        {
            get
            {
                var path = Path.Combine(Application.temporaryCachePath, "LastUnfocusedEventTime.json");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonUtility.FromJson<JsonDateTime>(json);
                }

                var time = DateTime.MinValue;
                LastUnfocusedEventTime = time;

                return time;
            }
            set
            {
                JsonDateTime jsonDateTime = value;
                var path = Path.Combine(Application.temporaryCachePath, "LastUnfocusedEventTime.json");
                var json = JsonUtility.ToJson(jsonDateTime);
                File.WriteAllText(path, json);
            }
        }

        static EditorUtils()
        {
            if (EditorApplication.update != null)
                EditorApplication.update -= OnEditorUpdate;

            EditorApplication.update += OnEditorUpdate;

            OnEditorFocusStateChanged += isFocused =>
            {
                if (isFocused) return;

                LastUnfocusedEventTime = DateTime.UtcNow;
            };
        }
        
        private static bool _previousFocusedState = true;
        private static void OnEditorUpdate()
        {
            if (_previousFocusedState == IsEditorFocused) return;

            _previousFocusedState = IsEditorFocused;
            OnEditorFocusStateChanged?.Invoke(_previousFocusedState);
        }
    }
}
