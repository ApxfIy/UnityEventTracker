using System;
using UnityEngine;

namespace UnityEventTracker.DataClasses
{
    [Serializable]
    internal struct Address
    {
        [SerializeField] private string _assetGuid;
        [SerializeField] private string _gameObjectId; // Local Id of related game object, 

        public string AssetGuid => _assetGuid;
        public string GameObjectId => _gameObjectId; 

        public Address(string assetGuid, string gameObjectId)
        {
            _assetGuid = assetGuid;
            _gameObjectId = gameObjectId;
        }

        public override string ToString()
        {
            return $"{AssetGuid} at {GameObjectId}";
        }

        public override bool Equals(object ob)
        {
            if (!(ob is Address address))
                return false;

            return address.AssetGuid == AssetGuid && address.GameObjectId == GameObjectId;
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                var hash = (int)2166136261;
                hash = (hash * 16777619) ^ AssetGuid.GetHashCode();
                hash = (hash * 16777619) ^ GameObjectId.GetHashCode();
                return hash;
            }
        }
    }
}