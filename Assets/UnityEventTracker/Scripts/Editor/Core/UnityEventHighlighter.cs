using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine.Events;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker
{
    [InitializeOnLoad]
    internal static class UnityEventHighlighter
    {
        private static readonly Type BaseUnityEventType = typeof(UnityEventBase);

        private static readonly FieldInfo PersistentCallsField =
            BaseUnityEventType.GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type PersistentCallGroupType = PersistentCallsField.FieldType;

        private static readonly MethodInfo GetListener =
            PersistentCallGroupType.GetMethod("GetListener", BindingFlags.Instance | BindingFlags.Public);

        private static readonly Type PersistentCallType = GetListener.ReturnType;

        private static readonly FieldInfo Mode =
            PersistentCallType.GetField("m_Mode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo Arguments =
            PersistentCallType.GetField("m_Arguments", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type ArgumentsType = Arguments.FieldType;

        private static readonly FieldInfo Argument = ArgumentsType.GetField("m_ObjectArgumentAssemblyTypeName",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type ObjectType         = typeof(object);
        private static readonly Type SerializeFieldType = typeof(SerializeField);

        private static readonly Dictionary<int, Item> ProceedObjects              = new Dictionary<int, Item>();
        private static readonly HashSet<int>          ProceedObjectsWithoutEvents = new HashSet<int>();
        private static          int[]                 _previousSelection;
        private static readonly List<int>             Deselected = new List<int>();

        private static readonly UnityEventTrackerSettings Settings;

        static UnityEventHighlighter()
        {
            Settings = UnityEventTrackerSettings.Instance();

            Settings.IsHighlightingEnabled.OnChange += isEnabled =>
            {
                if (isEnabled)
                    Initialize();
                else
                    DeInitialize();
            };

            Settings.InvalidCallColor.OnChange += color =>
            {
                if (!Settings.IsHighlightingEnabled) return;

                foreach (var item in ProceedObjects.Values.Where(item => !item.IsValid))
                    item.HierarchyItem.BackgroundColor = color;
            };

            Settings.ValidCallColor.OnChange += color =>
            {
                if (!Settings.IsHighlightingEnabled) return;

                foreach (var item in ProceedObjects.Values.Where(item => item.IsValid))
                    item.HierarchyItem.BackgroundColor = color;
            };

            if (!Settings.IsHighlightingEnabled) return;

            Initialize();
        }

        private static void Initialize()
        {
            if (EditorApplication.hierarchyWindowItemOnGUI != null)
                EditorApplication.hierarchyWindowItemOnGUI -= HandleHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;

            if (Selection.selectionChanged != null)
                Selection.selectionChanged -= SelectionChanged;
            Selection.selectionChanged += SelectionChanged;

            _previousSelection = Selection.instanceIDs;

            EditorApplication.RepaintHierarchyWindow();
        }

        private static void DeInitialize()
        {
            if (EditorApplication.hierarchyWindowItemOnGUI != null)
                EditorApplication.hierarchyWindowItemOnGUI -= HandleHierarchyWindowItemOnGUI;

            if (Selection.selectionChanged != null)
                Selection.selectionChanged -= SelectionChanged;

            EditorApplication.RepaintHierarchyWindow();
        }

        private static void SelectionChanged()
        {
            var currentSelection = Selection.instanceIDs;

            foreach (var instanceId in _previousSelection)
            {
                if (currentSelection.Contains(instanceId)) continue;

                Deselected.Add(instanceId);
            }

            _previousSelection = currentSelection;
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceId, Rect rect)
        {
            try
            {
                if (ProceedObjects.ContainsKey(instanceId))
                {
                    var item = ProceedObjects[instanceId];

                    if (Deselected.Contains(instanceId))
                    {
                        var go = item.HierarchyItem.GameObject;

                        // If gameObject was destroyed
                        if (go == null) return;

                        var isValidOptional = ValidateUnityEvents(go);

                        if (!isValidOptional.HasValue(out var isValid))
                        {
                            ProceedObjects.Remove(instanceId);
                            ProceedObjectsWithoutEvents.Add(instanceId);
                            return;
                        }

                        item.HierarchyItem.BackgroundColor = isValid ? Settings.ValidCallColor : Settings.InvalidCallColor;
                    }

                    item.HierarchyItem.UpdateRect(rect);
                    item.HierarchyItem.Draw();
                    return;
                }

                if (ProceedObjectsWithoutEvents.Contains(instanceId))
                    if (!Deselected.Remove(instanceId))
                        return;

                {
                    var instance = EditorUtility.InstanceIDToObject(instanceId);

                    if (instance == null) return;

                    var go = instance as GameObject;

                    if (go == null) return;

                    var isValidOptional = ValidateUnityEvents(go);

                    if (!isValidOptional.HasValue(out var isValid))
                    {
                        ProceedObjectsWithoutEvents.Add(instanceId);
                        return;
                    }

                    var hierarchyItem = new HierarchyItem(instanceId, rect, go)
                    {
                        BackgroundColor = isValid ? Settings.ValidCallColor : Settings.InvalidCallColor
                    };
                    hierarchyItem.Draw();

                    ProceedObjects.Add(instanceId, new Item(hierarchyItem, isValid));
                }
            }
            catch
            {
                // Ignores
            }
        }

        private static Optional<bool> ValidateUnityEvents(GameObject gameObject)
        {
            var components  = gameObject.GetComponents<MonoBehaviour>().Where(c => c != null).ToArray();
            var unityEvents = new List<Func<UnityEventBase>>();

            foreach (var component in components) unityEvents.AddRange(GetEvents(component));

            if (unityEvents.Count == 0)
                return Optional<bool>.FromNone();

            var isValid = true;

            foreach (var @event in unityEvents)
            {
                var e = @event();

                if (e == null) continue;

                var persistentCalls = PersistentCallsField.GetValue(e);
                var count           = e.GetPersistentEventCount();

                if (count == 0) continue;

                for (var i = 0; i < count; i++)
                {
                    var call = GetListener.Invoke(persistentCalls, new object[] {i});

                    var mode = (PersistentListenerMode) Mode.GetValue(call);

                    var methodName = e.GetPersistentMethodName(i);
                    var target     = e.GetPersistentTarget(i);

                    var arguments = Arguments.GetValue(call);

                    if (arguments == null) continue;

                    var desiredArgTypeName = (string) Argument.GetValue(arguments);
                    var desiredType        = ObjectType;

                    if (!string.IsNullOrEmpty(desiredArgTypeName))
                        desiredType = Type.GetType(desiredArgTypeName, false) ?? ObjectType;

                    isValid = UnityEventDrawer.IsPersistantListenerValid(e, methodName, target, mode, desiredType);

                    if (!isValid)
                        goto Finish;
                }
            }

            Finish:

            return Optional<bool>.FromSome(isValid);
        }

        private static IEnumerable<Func<UnityEventBase>> GetEvents(MonoBehaviour target)
        {
            var fields = target
                         .GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var result = new List<Func<UnityEventBase>>();

            foreach (var fieldInfo in fields)
            {
                if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, typeof(SerializeField))) continue;

                if (BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType))
                {
                    var info = fieldInfo;
                    result.Add(() => (UnityEventBase) info.GetValue(target));
                    continue;
                }

                if ((fieldInfo.FieldType.Attributes & TypeAttributes.Serializable) == 0) continue;

                result.AddRange(GetEventsRecursive(fieldInfo, fieldInfo.GetValue(target)));
            }

            return result;
        }

        private static IEnumerable<Func<UnityEventBase>> GetEventsRecursive(FieldInfo field, object parent,
            Dictionary<Type, int>                                                     visited = null)
        {
            if (parent == null)
                return new List<Func<UnityEventBase>>();

            var fields = field.FieldType
                              .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            visited ??= new Dictionary<Type, int> {{field.FieldType, 1}};

            var result = new List<Func<UnityEventBase>>();

            foreach (var fieldInfo in fields)
            {
                if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, typeof(SerializeField))) continue;

                if (BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType))
                {
                    var info = fieldInfo;
                    result.Add(() => (UnityEventBase) info.GetValue(parent));
                    continue;
                }

                if ((fieldInfo.FieldType.Attributes & TypeAttributes.Serializable) == 0) continue;

                if (visited.ContainsKey(fieldInfo.FieldType))
                {
                    if (visited[fieldInfo.FieldType] >= 10)
                        continue;

                    visited[fieldInfo.FieldType]++;
                }
                else
                {
                    visited.Add(fieldInfo.FieldType, 1);
                }

                result.AddRange(GetEventsRecursive(fieldInfo, fieldInfo.GetValue(parent), visited));
            }

            return result;
        }

        private class Item
        {
            public HierarchyItem HierarchyItem { get; }
            public bool          IsValid       { get; }

            public Item(HierarchyItem hierarchyItem, bool isValid)
            {
                HierarchyItem = hierarchyItem;
                IsValid       = isValid;
            }
        }
    }
}