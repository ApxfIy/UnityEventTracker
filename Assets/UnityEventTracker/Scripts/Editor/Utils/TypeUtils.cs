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
        /// Search field with specified <paramref name="name"/>. Also checks for <see cref="FormerlySerializedAsAttribute"/>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static Optional<FieldInfo> GetSerializedField(Type type, string name)
        {
            name = name.Trim(); //TODO
            var fieldNames = name.Split(":");

            foreach (var fieldName in fieldNames)
            {
                var fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (fieldInfo != null && (fieldInfo.IsPublic || fieldInfo.IsDefined(SerializeFieldType)))
                    return Optional<FieldInfo>.FromSome(fieldInfo);

                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    var attribute = field.GetCustomAttribute<FormerlySerializedAsAttribute>();

                    if (attribute == null) continue;
                    if (!attribute.oldName.Equals(fieldName)) continue;
                    if (!field.IsPublic && !field.IsDefined(SerializeFieldType)) continue;

                    return Optional<FieldInfo>.FromSome(field);
                }
            }

            return Optional<FieldInfo>.FromNone();
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

            for (var i = 0; i < fields.Length; i++)
            {
                var fieldInfo = fields[i];

                if (visited.Contains(fieldInfo.FieldType)) continue;

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

