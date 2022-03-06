using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEventTracker.DataClasses
{
    [Serializable]
    internal class PersistentCall
    {
        internal enum PersistentCallState
        {
            Valid,
            InvalidTarget,
            InvalidArgument,
            InvalidMethod
        }

        [SerializeField] private Address _address;
        [SerializeField] private ObjectReference _targetInfo;
        [SerializeField] private ObjectReference _argumentInfo;
        [SerializeField] private string _methodName;
        [SerializeField] private PersistentListenerMode _listenerMode;
        [SerializeField] private string[] _argTypes;
        [SerializeField] private string _eventName;
        [SerializeField] private string _eventScriptGuid;
        [SerializeField] private int _methodLine;
        [SerializeField] private PersistentCallState _state;

        public Address Address => _address;
        public ObjectReference TargetInfo => _targetInfo;
        public ObjectReference ArgumentInfo => _argumentInfo;
        public string MethodName => _methodName;
        public PersistentListenerMode ListenerMode => _listenerMode;
        public string[] ArgTypes => _argTypes;
        public string EventName => _eventName;
        public string EventScriptGuid => _eventScriptGuid;
        public int MethodLine => _methodLine;
        public PersistentCallState State => _state;

        public PersistentCall(
            Address address,
            ObjectReference targetInfo,
            ObjectReference argumentInfo,
            string methodName,
            PersistentListenerMode listenerMode,
            string[] argTypes,
            string eventName,
            string eventScriptGuid,
            int methodLine,
            PersistentCallState persistentCallState)
        {
            _address = address;
            _targetInfo = targetInfo;
            _argumentInfo = argumentInfo;
            _methodName = methodName;
            _listenerMode = listenerMode;
            _argTypes = argTypes;
            _eventName = eventName;
            _eventScriptGuid = eventScriptGuid;
            _methodLine = methodLine;
            _state = persistentCallState;
        }

        public PersistentCall Copy(PersistentCallState state = (PersistentCallState)(-1))
        {
            var args = _argTypes == null ? null : new string[_argTypes.Length];
            state = (int) state == -1 ? _state : state; 

            if (args != null)
                Array.Copy(_argTypes, args, _argTypes.Length);

            return new PersistentCall(_address, _targetInfo, _argumentInfo, _methodName, _listenerMode, args,
                _eventName, _eventScriptGuid, _methodLine, state);
        }

        public Optional<Type> GetTargetType()
        {
            if (TargetInfo.IsUnityType())
            {
                var type = Type.GetType(TargetInfo.AssemblyTypeName);
                return Optional<Type>.FromSome(type);
            }
            else
            {
                var path = AssetDatabase.GUIDToAssetPath(TargetInfo.ScriptGuid);
                Type targetType;

                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (monoScript != null && monoScript.GetClass() != null)
                {
                    targetType = monoScript.GetClass();
                }
                else
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                    if (asset == null)
                        return Optional<Type>.FromNone();

                    targetType = asset.GetType();
                }

                return Optional<Type>.FromSome(targetType);
            }
        }
    }
}