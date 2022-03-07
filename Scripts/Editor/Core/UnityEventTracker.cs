using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEventTracker.DataClasses;
using UnityEventTracker.EditorWindow;
using UnityEventTracker.Serialization;
using UnityEventTracker.Utils;
using Asset = UnityEventTracker.Utils.Asset;
using Logger = UnityEventTracker.Utils.Logger;

[assembly: InternalsVisibleTo("UnityEventTracker.Tests")]
namespace UnityEventTracker
{
    [InitializeOnLoad]
    internal static class UnityEventTracker
    {
        public static event Action OnDataChanged;
        public const string Name = "UnityEventTracker";
        public static readonly string RootPath = Path.Combine(Application.dataPath, "Plugins/UnityEventTracker");

        private static readonly Type MonoBehaviourType = typeof(MonoBehaviour);
        private static readonly Type ScriptableObjectType = typeof(ScriptableObject);

        internal static readonly PersistentCallsCollection CallsContainer =
            new PersistentCallsCollection("PersistentCalls", RootPath);

        private static readonly SerializableHashSet<string> ScriptsWithEvents =
            new SerializableHashSet<string>("ScriptsWithEvents", RootPath);

        // For scripts that was imported outside of Unity when project wasn't open
        private static readonly SerializableHashSet<string> ScriptsToCheck =
            new SerializableHashSet<string>("ScriptsToCheck", RootPath);

        private static readonly UnityEventTrackerSettings Settings;

        [MenuItem("Tools/UnityEventTracker/Scan Project")]
        private static bool ScanProject()
        {
            var isSaved = AssetUtils.AskUserToSaveModifiedAssets(2, "All assets need to be saved before scanning");

            if (!isSaved)
                return false;

            var scripts = AssetUtils.GetAllScriptAssets().Where(s => IsValidType(s.Type));
            var classesWithEvents = AssetUtils.GetAllClassesWithEventsGuid(scripts);

            ScriptsWithEvents.SetData(classesWithEvents.Select(c => c.Guid).ToHashSet());

            var assets = AssetUtils.GetAllAssetOfTypeInTheProject("t:ScriptableObject t:prefab", "Assets")
                .Where(a => !Settings.ShouldSkipFolder(a.RelativePath))
                .Concat(AssetUtils.GetAllScenesInBuild()).ToArray();

            var canceled = false;
            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];

                CallsContainer.RemoveAllEventDataInAsset(asset.Guid);

                var calls = new EventParser(asset, DoesClassHasEvents).Parse();

                CallsContainer.AddRange(calls);

                canceled = EditorUtility.DisplayCancelableProgressBar("Scanning assets",
                    "Scanning all assets in the project", (float)i / assets.Length);

                if (!canceled) continue;

                break;
            }

            CheckLogs();

            CallsContainer.Save();
            ScriptsWithEvents.Save();

            EditorUtility.ClearProgressBar();

            DataChanged();

            return !canceled;
        }

        static UnityEventTracker()
        {
            Settings = UnityEventTrackerSettings.Instance();

            Settings.IsTrackingEnabled.OnChange += isEnabled =>
            {
                if (isEnabled)
                    Initialize();
                else
                    DeInitialize();
            };

            if (!Settings.IsTrackingEnabled) return;

            Initialize();
        }

        [DidReloadScripts]
        private static void OnRecompile()
        {
            var changedScripts =
                AssetUtils.GetAllChangedScripts()
                    .Where(s => IsValidType(s.Type)).ToArray();

            if (changedScripts.Length == 0) return;

            UpdateScriptsWithEventsList(changedScripts);

            ValidateAllMethodsInClasses(changedScripts);
        }

        private static void Initialize()
        {
            if (!Settings.InInitialScanComplete)
            {
                // To bypass "InvalidOperationException: Calling Scene Raise from assembly reloading callbacks are not supported."
                static void DelayedScan()
                {
                    var isScanComplete = ScanProject();
                    Settings.InInitialScanComplete = isScanComplete;
                    EditorApplication.update -= DelayedScan;
                }

                EditorApplication.update += DelayedScan;
            }

            AssetModificationEvents.OnBeforeCreate += AssetModificationEventsOnBeforeCreate;
            AssetModificationEvents.OnBeforeDelete += AssetModificationEventsOnBeforeDelete;

            AssetPostprocessorEvents.OnAssetsImported += AssetPostprocessorEventsOnAssetsImported;
        }

        private static void DeInitialize()
        {
            AssetModificationEvents.OnBeforeCreate -= AssetModificationEventsOnBeforeCreate;
            AssetModificationEvents.OnBeforeDelete -= AssetModificationEventsOnBeforeDelete;

            AssetPostprocessorEvents.OnAssetsImported -= AssetPostprocessorEventsOnAssetsImported;
        }

        private static void AssetPostprocessorEventsOnAssetsImported(string[] paths)
        {
            var changed = false;

            foreach (var path in paths.Where(p => p.StartsWith("Assets")))
            {
                if (!IsControlledAsset(path)) continue;

                if (Settings.ShouldSkipFolder(path)) continue;

                var asset = Asset.FromRelativePath(path).GetValueUnsafe();

                CallsContainer.RemoveAllEventDataInAsset(asset.Guid);

                var calls = new EventParser(asset, DoesClassHasEvents).Parse();

                foreach (var persistentCall in calls)
                {
                    CallsContainer.Add(persistentCall);
                }

                changed = true;
            }

            if (!changed) return;

            CallsContainer.Save();

            DataChanged();
        }

        private static void AssetModificationEventsOnBeforeDelete(string relativePath)
        {
            if (Settings.ShouldSkipFolder(relativePath)) return;

            //It's a script meta file
            if (relativePath.Contains(".cs"))
            {
                var scriptRelativePath = relativePath.Substring(0, relativePath.Length - 5);
                var guid = AssetDatabase.AssetPathToGUID(scriptRelativePath);

                ScriptsWithEvents.Remove(guid);

                CallsContainer.RemoveAllMethodsFrom(guid);

                DataChanged();
            }
            else if (IsControlledAsset(relativePath))
            {
                var asset = Asset.FromRelativePath(relativePath).GetValueUnsafe();
                CallsContainer.RemoveAllEventDataInAsset(asset.Guid);

                DataChanged();
            }
        }

        private static void AssetModificationEventsOnBeforeCreate(string relativePath)
        {
            if (!relativePath.Contains(".cs")) return;

            if (Settings.ShouldSkipFolder(relativePath)) return;

            var scriptRelativePath = relativePath.Substring(0, relativePath.Length - 5);
            ScriptsToCheck.Add(scriptRelativePath);
        }

        private static bool IsControlledAsset(string path)
        {
            return path.EndsWith(".unity") || path.EndsWith(".prefab") ||
                   path.EndsWith(".asset");
        }

        private static bool DoesClassHasEvents(string classGuid)
        {
            return ScriptsWithEvents.Contains(classGuid);
        }

        private static void UpdateScriptsWithEventsList(IEnumerable<ScriptAsset> changedScripts)
        {
            foreach (var scriptAsset in changedScripts)
            {
                var hasEvents = TypeUtils.HasEvents(scriptAsset);

                if (!hasEvents)
                    ScriptsWithEvents.Remove(scriptAsset.Guid);
                else
                    ScriptsWithEvents.Add(scriptAsset.Guid);
            }

            foreach (var scriptRelativePath in ScriptsToCheck)
            {
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptRelativePath);

                var type = monoScript.GetClass();

                if (!IsValidType(type)) return;

                var hasEvents = TypeUtils.HasEvents(type);
                var guid = AssetDatabase.AssetPathToGUID(scriptRelativePath);

                if (hasEvents)
                    ScriptsWithEvents.Add(guid);
                else
                    ScriptsWithEvents.Remove(guid);
            }

            ScriptsToCheck.SetData(new HashSet<string>());
            ScriptsWithEvents.Save();
        }

        private static void ValidateAllMethodsInClasses(IEnumerable<ScriptAsset> classes)
        {
            var changed = false;

            foreach (var scriptAsset in classes)
            {
                var isUsed = CallsContainer.DoesScriptIsUsedInEvents(scriptAsset.Guid);

                if (!isUsed) continue;

                var allCalls = CallsContainer.GetAllCalls();

                for (var i = 0; i < allCalls.Count; i++)
                {
                    var call = allCalls[i];

                    if (call.TargetInfo.IsUnityType() || !call.TargetInfo.ScriptGuid.Equals(scriptAsset.Guid))
                        continue;

                    var isValid = PersistentCallUtils.ValidateMethodCall(call);

                    if (call.State == PersistentCall.PersistentCallState.Valid && isValid)
                        continue;

                    CallsContainer.Remove(call);

                    var newState = call.State != PersistentCall.PersistentCallState.InvalidTarget
                        ? isValid ? PersistentCall.PersistentCallState.Valid :
                                    PersistentCall.PersistentCallState.InvalidMethod
                        : PersistentCall.PersistentCallState.InvalidTarget;
                    var newCall = call.Copy(newState);

                    CallsContainer.Add(newCall);

                    changed = true;
                }
            }

            if (!changed) return;

            CallsContainer.Save();

            DataChanged();
        }

        private static void CheckLogs()
        {
            if (!Logger.IsFull()) return;

            var accepted = EditorUtility.DisplayDialog(Name, "Some errors occured " +
                                                                            "during project scan. You can send me the logs (open a new issue on Github) so I can take a look. " +
                                                                            "Please, note, that the log files contains full YAML representation of asset (scene, prefab or SO) that " +
                                                                            "failed to be scanned, so it may contain some private information (API keys, passwords etc.) that should " +
                                                                            "be removed from the log files before sending them", "Show Logs Location");

            if (!accepted) return;

            var absolutePath = Logger.GetBugReportPaths().First();
            var offset = absolutePath.IndexOf("Assets", StringComparison.InvariantCulture);
            var relativePath = absolutePath.Substring(offset);
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);

            Selection.activeObject = file;
            EditorGUIUtility.PingObject(file);
        }

        private static bool IsValidType(Type type)
        {
            return IsValidMBType(type) || IsValidSOType(type);
        }

        private static bool IsValidMBType(Type type)
        {
            return type != null && !type.IsAbstract && MonoBehaviourType.IsAssignableFrom(type);
        }

        private static bool IsValidSOType(Type type)
        {
            return type != null && !type.IsAbstract && ScriptableObjectType.IsAssignableFrom(type);
        }

        private static void DataChanged()
        {
            OnDataChanged?.Invoke();

            if (!Settings.OpenWindowAutomatically) return;

            if (CallsContainer.Any(c => c.State != PersistentCall.PersistentCallState.Valid))
                UnityEventHelperWindow.Open();
        }
    }
}