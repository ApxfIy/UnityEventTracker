using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace UnityEventTracker.Utils
{
    public static class TypeUtils
    {
        internal static readonly Type SerializeFieldType = typeof(SerializeField);
        internal static readonly Type BaseUnityEventType = typeof(UnityEventBase);

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
            var fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo != null && (fieldInfo.IsPublic || fieldInfo.IsDefined(SerializeFieldType)))
                return Optional<FieldInfo>.FromSome(fieldInfo);
            
            var fields = type.GetFields(); 
            
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<FormerlySerializedAsAttribute>();

                if (attribute == null) continue;
                if (!attribute.oldName.Equals(name)) continue;
                if (!field.IsPublic || !field.IsDefined(SerializeFieldType)) continue;

                return Optional<FieldInfo>.FromSome(field);
            }

            return Optional<FieldInfo>.FromNone();
        }

        internal static bool HasEvents(ScriptAsset scriptAsset)
        {
            return HasEvents(scriptAsset.Script.GetClass());
        }

        internal static bool HasEvents(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (var i = 0; i < fields.Length; i++)
            {
                var fieldInfo = fields[i];
                
                if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, SerializeFieldType)) continue;

                if (BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType))
                    return true;
            }

            return false;
        }
    }
}

