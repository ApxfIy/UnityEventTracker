using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace UnityEventTracker.Utils
{
    public static class TypeUtils
    {
        private static readonly Type SerializeFieldType = typeof(SerializeField);
        private static readonly Type BaseUnityEventType = typeof(UnityEventBase);
        private static readonly Type MonoBehaviourType    = typeof(MonoBehaviour);
        private static readonly Type ScriptableObjectType = typeof(ScriptableObject);

        public static bool CheckTypeMatch(Type type, PersistentListenerMode mode)
        {
            switch (mode)
            {
                case PersistentListenerMode.Void when type == typeof(void):
                case PersistentListenerMode.Object when type == typeof(object):
                case PersistentListenerMode.Int when type == typeof(int):
                case PersistentListenerMode.Float when type == typeof(float):
                case PersistentListenerMode.String when type == typeof(string):
                case PersistentListenerMode.Bool when type == typeof(bool):
                    return true;
                default:
                    return false;
            }
        }

        public static Type PersistentListenerModeToType(PersistentListenerMode mode)
        {
            switch (mode)
            {
                case PersistentListenerMode.Void:
                    return typeof(void);
                case PersistentListenerMode.Int:
                    return typeof(int);
                case PersistentListenerMode.Float:
                    return typeof(float);
                case PersistentListenerMode.String:
                    return typeof(string);
                case PersistentListenerMode.Bool:
                    return typeof(bool);
                default:
                    return typeof(object);
            }
        }

        public static PersistentListenerMode TypeToPersistentListenerMode(Type type)
        {
            if (type == typeof(void))
                return PersistentListenerMode.Void;
            if (type == typeof(int))
                return PersistentListenerMode.Int;
            if (type == typeof(float))
                return PersistentListenerMode.Float;
            if (type == typeof(string))
                return PersistentListenerMode.String;
            if (type == typeof(bool))
                return PersistentListenerMode.Bool;

            return PersistentListenerMode.Object;
        }

        /// <summary>
        /// Search field with specified <paramref name="fieldPath"/>. Also checks for <see cref="FormerlySerializedAsAttribute"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fieldPath"></param>
        /// <returns></returns>
        public static Optional<FieldInfo> GetSerializedField(Type type, string fieldPath)
        {
            fieldPath = fieldPath.Trim(); //TODO
            var fieldNames = fieldPath.Split(":");
            
            var parent = type;
            FieldInfo result = null;

            foreach (var fieldName in fieldNames)
            {
                var fieldInfo = parent.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (fieldInfo != null && (fieldInfo.IsPublic || fieldInfo.IsDefined(SerializeFieldType)))
                {
                    parent = fieldInfo.FieldType;
                    result = fieldInfo;
                    continue;
                }

                var fields = parent.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    var attribute = field.GetCustomAttribute<FormerlySerializedAsAttribute>();

                    if (attribute == null) continue;
                    if (!attribute.oldName.Equals(fieldName)) continue;
                    if (!field.IsPublic && !field.IsDefined(SerializeFieldType)) continue;
                    
                    parent = field.FieldType;
                    result = field;
                    goto Next;
                }

                return Optional<FieldInfo>.FromNone();

                Next: ;
            }

            return result == null ? Optional<FieldInfo>.FromNone() : Optional<FieldInfo>.FromSome(result);
        }

        internal static bool HasEvents(ScriptAsset scriptAsset)
        {
            return HasEvents(scriptAsset.Script.GetClass());
        }
        
        internal static bool HasEvents(Type type)
        {
            if (!IsValidMBType(type) && !IsValidSOType(type))
                return false;

            return HasEventsInternal(type);
        }

        private static bool HasEventsInternal(Type type, HashSet<Type> visited = null)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // To break recursive dependencies
            visited ??= new HashSet<Type> { type };

            foreach (var fieldInfo in fields)
            {
                if (visited.Add(fieldInfo.FieldType)) continue;
                
                if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, typeof(SerializeField))) continue;

                if (BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType))
                    return true;

                if ((fieldInfo.FieldType.Attributes & TypeAttributes.Serializable) == 0) continue;

                return HasEventsInternal(fieldInfo.FieldType, visited);
            }

            return false;
        }

        private static bool IsValidMBType(Type type)
        {
            return type != null && !type.IsAbstract && MonoBehaviourType.IsAssignableFrom(type);
        }

        private static bool IsValidSOType(Type type)
        {
            return type != null && !type.IsAbstract && ScriptableObjectType.IsAssignableFrom(type);
        }
    }
}

