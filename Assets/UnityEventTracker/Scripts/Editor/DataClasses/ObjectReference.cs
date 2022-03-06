using System;
using UnityEngine;

namespace UnityEventTracker.DataClasses
{
    [Serializable]
    internal struct ObjectReference
    {
        [SerializeField] private string _scriptGuid;
        [SerializeField] private string _assemblyTypeName;

        [SerializeField] private string _fileId;
        [SerializeField] private string _assetGuid;
        [SerializeField] private bool _isLocal;

        [SerializeField] public string ScriptGuid => _scriptGuid;
        [SerializeField] public string AssemblyTypeName => _assemblyTypeName;

        private ObjectReference(string scriptGuid, string assemblyTypeName, string fileId, string assetGuid, bool isLocal)
        {
            _scriptGuid = scriptGuid;
            _assemblyTypeName = assemblyTypeName;
            _fileId = fileId;
            _assetGuid = assetGuid;
            _isLocal = isLocal;
        }

        public static ObjectReference FromLocal(string fileId, string scriptGuid, string assemblyTypeName)
        {
            return new ObjectReference(scriptGuid, assemblyTypeName, fileId, null, true);
        }

        public static ObjectReference FromGlobal(string assetGuid, string scriptGuid, string assemblyTypeName)
        {
            return new ObjectReference(scriptGuid, assemblyTypeName, null, assetGuid, false);
        }

        public bool IsLocal(out string fileId)
        {
            fileId = _fileId;
            return _isLocal;
        }

        public bool IsGlobal(out string assetId)
        {
            assetId = _assetGuid;
            return !_isLocal;
        }

        public bool IsUnityType()
        {
            return string.IsNullOrEmpty(ScriptGuid);
        }
    }
}