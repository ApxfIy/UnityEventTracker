using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.Serialization
{
    internal class PersistentCallsCollection : IEnumerable<PersistentCall>
    {
        public string Name { get; }
        public string RootPath { get; }

        private List<PersistentCall> _persistentCalls;

        public static implicit operator List<PersistentCall>(PersistentCallsCollection collection)
        {
            return collection._persistentCalls;
        }
        
        public PersistentCallsCollection(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
            _persistentCalls = Load();
        }

        public void Add(PersistentCall call)
        {
            _persistentCalls.Add(call);
        }

        public void AddRange(IEnumerable<PersistentCall> calls)
        {
            _persistentCalls.AddRange(calls);
        }

        public bool Remove(PersistentCall call)
        {
            return _persistentCalls.Remove(call);
        }

        public void RemoveAllMethodsFrom(string scriptGuid)
        {
            _persistentCalls = _persistentCalls
                .Where(c => c.TargetInfo.IsUnityType() || !c.TargetInfo.ScriptGuid.Equals(scriptGuid)).ToList();
        }

        public bool DoesScriptIsUsedInEvents(string scriptGuid)
        {
            return _persistentCalls.Any(c => !c.TargetInfo.IsUnityType() && c.TargetInfo.ScriptGuid.Equals(scriptGuid));
        }

        public IEnumerable<PersistentCall> GetAllCalls(string scriptGuid)
        {
            return _persistentCalls.Where(c => !c.TargetInfo.IsUnityType() && c.TargetInfo.ScriptGuid.Equals(scriptGuid));
        }

        public IReadOnlyList<PersistentCall> GetAllCalls()
        {
            return _persistentCalls;
        }

        public void RemoveAllEventDataInAsset(string assetGuid)
        {
            _persistentCalls = _persistentCalls.Where(c => !c.Address.AssetGuid.Equals(assetGuid)).ToList(); 
        }

        public void Save()
        {
            var filePath = GetFilePath();

            CreateDirectoryIfNotExist(filePath);

            var serializedData = JsonUtility.ToJson(new Wrapper<List<PersistentCall>>(_persistentCalls));
            File.WriteAllText(filePath, serializedData);
        }

        private List<PersistentCall> Load()
        {
            var filePath = GetFilePath();

            if (!File.Exists(filePath)) 
                return new List<PersistentCall>();

            var fileContent = File.ReadAllText(filePath);

            var container = JsonUtility.FromJson<Wrapper<List<PersistentCall>>>(fileContent);
            return container?.Data ?? new List<PersistentCall>();
        }

        private static void CreateDirectoryIfNotExist(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var directory = fileInfo.Directory;
            directory.Create();
        }

        private string GetFilePath()
        {
            return Path.Combine(RootPath, $"{Name}.json").Replace("\\", "/");
        }

        public IEnumerator<PersistentCall> GetEnumerator()
        {
            return _persistentCalls.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}