using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEventTracker.Serialization
{
    internal class SerializableHashSet<TArgs> : IEnumerable<TArgs>
    {
        public string Name { get; }
        public string RootPath { get; }

        private HashSet<TArgs> _hashSet;
        
        public SerializableHashSet(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
            var serializedData = Load();
            _hashSet = serializedData.ToHashSet();
        }
        
        public bool Add(TArgs element)
        {
            return _hashSet.Add(element);
        }

        public bool Contains(TArgs element)
        {
            return _hashSet.Contains(element);
        }

        public bool Remove(TArgs element)
        {
            return _hashSet.Remove(element);
        }

        public void SetData(IEnumerable<TArgs> newData)
        {
            if (newData is HashSet<TArgs> hashSet)
                _hashSet = hashSet;
            else
                _hashSet = newData.ToHashSet();

            Save();
        }

        public void Save()
        {
            var filePath = GetFilePath();

            CreateDirectoryIfNotExist(filePath);

            var serializedData = JsonUtility.ToJson(new Wrapper<List<TArgs>>(_hashSet.ToList()));
            File.WriteAllText(filePath, serializedData);
        }

        protected List<TArgs> Load()
        {
            var filePath = GetFilePath();

            if (!File.Exists(filePath)) 
                return new List<TArgs>();

            var fileContent = File.ReadAllText(filePath);
            var container = JsonUtility.FromJson<Wrapper<List<TArgs>>>(fileContent);
            return container?.Data ?? new List<TArgs>();
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

        public IEnumerator<TArgs> GetEnumerator()
        {
            return _hashSet.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
