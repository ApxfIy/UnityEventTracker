using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.Utils
{
    internal struct ScriptAsset
    {
        public string Guid { get; }
        public MonoScript Script { get; }
        public Type Type => _cachedType ??= Script.GetClass();

        private Type _cachedType;

        private ScriptAsset(MonoScript script, string guid)
        {
            Script = script;
            Guid = guid;
            _cachedType = null;
        }

        public static Optional<ScriptAsset> FromGuid(string guid)
        {
            var relativeAssetPath = AssetDatabase.GUIDToAssetPath(guid);

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(relativeAssetPath);

            if (monoScript == null || monoScript.GetClass() == null)
            {
                return Optional<ScriptAsset>.FromNone();
            }

            var scriptAsset = new ScriptAsset(monoScript, guid);

            return Optional<ScriptAsset>.FromSome(scriptAsset);
        }
    }

    internal enum AssetType
    {
        None,
        Scene,
        Prefab,
        ScriptableObject
    }

    internal struct Asset
    {
        public string Guid { get; }
        public string AbsolutePath { get; }
        public string RelativePath { get; }
        public AssetType AssetType { get; }

        private static readonly string PathToTheProject =
            Application.dataPath.Substring(0,
                Application.dataPath.LastIndexOf("Assets", StringComparison.InvariantCulture));

        public static Optional<Asset> FromGuid(string guid)
        {
            var relativePath = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(relativePath))
                return Optional<Asset>.FromNone();

            var asset = new Asset
            (
                guid,
                relativePath,
                GetAbsolutePath(relativePath),
                GetAssetType(relativePath)
            );

            return Optional<Asset>.FromSome(asset);
        }

        public static Optional<Asset> FromRelativePath(string relativePath)
        {
            var guid = AssetDatabase.AssetPathToGUID(relativePath);

            return FromGuid(guid);
        }

        public static Optional<Asset> FromAbsolutePath(string absolutePath)
        {
            var offset = absolutePath.IndexOf("Assets", StringComparison.InvariantCulture) + "Assets".Length;
            var relativePath = absolutePath.Substring(offset);

            return FromRelativePath(relativePath);
        }

        public bool IsLoaded()
        {
            switch (AssetType)
            {
                case AssetType.Scene:
                    {
                        var isPrefabOpened = PrefabStageUtility.GetCurrentPrefabStage() != null;

                        if (isPrefabOpened)
                            return false;

                        var countLoaded = SceneManager.sceneCount;

                        for (var i = 0; i < countLoaded; i++)
                        {
                            if (!SceneManager.GetSceneAt(i).path.Equals(RelativePath)) continue;

                            return true;
                        }

                        return false;
                    }
                case AssetType.Prefab:
                    {
                        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                        var isPrefabLoaded = prefabStage != null && prefabStage.assetPath.Equals(RelativePath);

                        return isPrefabLoaded;
                    }
                default:
                    return false;
            }
        }

        public bool IsDirty()
        {
            switch (AssetType)
            {
                case AssetType.Scene:
                {
                    var countLoaded = SceneManager.sceneCount;

                    for (var i = 0; i < countLoaded; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (!scene.path.Equals(RelativePath)) continue;

                        return scene.isDirty;
                    }

                    return false;
                }
                case AssetType.Prefab:
                {
                    var prefabStage    = PrefabStageUtility.GetCurrentPrefabStage();
                    var isPrefabLoaded = prefabStage != null && prefabStage.assetPath.Equals(RelativePath);

                    return isPrefabLoaded && prefabStage.scene.isDirty;
                }
                default:
                    return false;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Asset asset))
                return false;

            return asset.Guid.Equals(Guid);
        }

        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        public IEnumerable<YAMLObject> GetObjects()
        {
            var data = File.ReadAllLines(AbsolutePath);

            bool IsComponent(int index)
            {
                var line = data[index];

                return line.StartsWith('-');
            }

            YAMLObject GetComponent(ref int index)
            {
                //It's this line: ---!u!1 & 1283144012
                var startIndex = index;

                //It's this line: GameObject:
                index++;

                do
                {
                    index++;
                } while (index < data.Length && data[index].Length > 0 && data[index][0] == ' ');

                var content = new ArraySegment<string>(data, startIndex, index - startIndex);

                index--;

                return new YAMLObject
                (
                    content,
                    startIndex
                );
            }

            for (var i = 0; i < data.Length; i++)
            {
                if (!IsComponent(i)) continue;

                yield return GetComponent(ref i);
            }
        }

        private Asset(string guid, string relativePath, string absolutePath, AssetType assetType)
        {
            Guid = guid;
            RelativePath = relativePath;
            AbsolutePath = absolutePath;
            AssetType = assetType;
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.Combine(PathToTheProject, relativePath);
        }

        private static AssetType GetAssetType(string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            if (fileName.Contains(".unity"))
                return AssetType.Scene;
            if (fileName.Contains(".prefab"))
                return AssetType.Prefab;
            if (fileName.Contains(".asset"))
                return AssetType.ScriptableObject;
            return AssetType.None;
        }
    }

    internal static class AssetUtils
    {
        internal static Asset[] GetAllScenesInBuild()
        {
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var scenes = new Asset[sceneCount];

            for (var i = 0; i < sceneCount; i++)
            {
                var relativePath = SceneUtility.GetScenePathByBuildIndex(i);

                if (Asset.FromRelativePath(relativePath).HasValue(out var asset))
                    scenes[i] = asset;
            }

            return scenes;
        }

        internal static Asset[] GetAllPrefabsInTheProject()
        {
            return GetAllAssetOfTypeInTheProject("t:prefab");
        }

        internal static Asset[] GetAllScriptableObjectsInTheProject()
        {
            return GetAllAssetOfTypeInTheProject("t:ScriptableObject");
        }

        /// <summary>
        /// Returns an array of absolute paths for each asset of the specified type in the project.
        /// Examples of <paramref name="searchPattern"/>: 
        ///     - t:ScriptableObject
        ///     - t:prefab
        ///     - t:scene
        /// </summary>
        /// <param name="searchPattern"><example>"t:prefab"</example></param>
        /// <returns></returns>
        internal static Asset[] GetAllAssetOfTypeInTheProject(string searchPattern, params string[] folders)
        {
            var GUIDs = AssetDatabase.FindAssets(searchPattern, folders);
            var assets = GUIDs.Select(g => Asset.FromGuid(g).GetValueUnsafe()).ToArray();

            return assets;
        }

        internal static IEnumerable<ScriptAsset> GetAllClassesWithEventsGuid(IEnumerable<ScriptAsset> scripts)
        {
            foreach (var script in scripts)
            {
                var type = script.Script.GetClass();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (var i = 0; i < fields.Length; i++)
                {
                    var fieldInfo = fields[i];

                    if (!TypeUtils.BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType)) continue;

                    if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, TypeUtils.SerializeFieldType)) continue;

                    yield return script;

                    break;
                }
            }
        }

        internal static IEnumerable<ScriptAsset> GetAllChangedScripts()
        {
            var GUIDs = AssetDatabase.FindAssets("t:script");
            var scripts = new List<ScriptAsset>(GUIDs.Length);

            for (var i = 0; i < GUIDs.Length; i++)
            {
                var guid = GUIDs[i];
                var relativeAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                var pathToProject =
                    Application.dataPath.Substring(0,
                        Application.dataPath.LastIndexOf("Assets", StringComparison.InvariantCulture));
                var absolutePath = Path.Combine(pathToProject, relativeAssetPath);

                var changeTime = File.GetLastWriteTime(absolutePath);

                if (changeTime < EditorUtils.LastUnfocusedEventTime) continue;

                if (!ScriptAsset.FromGuid(guid).HasValue(out var scriptAsset)) continue;

                scripts.Add(scriptAsset);
            }

            return scripts;
        }

        internal static IEnumerable<ScriptAsset> GetAllScriptAssets(params string[] searchInFolder)
        {
            var GUIDs = AssetDatabase.FindAssets("t:script", searchInFolder);

            for (var i = 0; i < GUIDs.Length; i++)
            {
                var guid = GUIDs[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.StartsWith("Assets/Plugins") || path.StartsWith("Assets/Samples")) continue;

                if (!ScriptAsset.FromGuid(guid).HasValue(out var scriptAsset)) continue;

                yield return scriptAsset;
            }
        }

        internal static bool AskUserToSaveModifiedAssets(int attempts, string failureMessage)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (SaveAllModifiedAssets()) return true;

                if (i == attempts - 1) break;

                var isAccepted = EditorUtility.DisplayDialog("SaveDialog",
                    failureMessage,
                    "Ok");

                if (isAccepted) continue;

                break;
            }

            return false;
        }

        internal static bool SaveAllModifiedAssets()
        {
            AssetDatabase.SaveAssets();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var prefabPath = prefabStage?.assetPath;

            if (prefabStage != null && prefabStage.scene.isDirty)
            {
                var isAccepted = EditorUtility.DisplayDialog("SaveDialog",
                    "Save modified prefab?",
                    "Save");

                if (!isAccepted)
                    return false;

                prefabStage.SaveUsingReflection();
            }

            var isSaved = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            if (isSaved)
            {
                EditorSceneManager.OpenScene(SceneManager.GetActiveScene().path);

                if (!string.IsNullOrEmpty(prefabPath))
                    PrefabStageUtility.OpenPrefab(prefabPath);

                return true;
            }

            return false;
        }
    }
}

