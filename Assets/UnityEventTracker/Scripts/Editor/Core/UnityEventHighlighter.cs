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
        private static readonly FieldInfo PersistentCallsField = BaseUnityEventType.GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type PersistentCallGroupType = PersistentCallsField.FieldType;
        private static readonly MethodInfo GetListener = PersistentCallGroupType.GetMethod("GetListener", BindingFlags.Instance | BindingFlags.Public);
        private static readonly Type PersistentCallType = GetListener.ReturnType;
        private static readonly FieldInfo Mode = PersistentCallType.GetField("m_Mode", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Arguments = PersistentCallType.GetField("m_Arguments", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type ArgumentsType = Arguments.FieldType;
        private static readonly FieldInfo Argument = ArgumentsType.GetField("m_ObjectArgumentAssemblyTypeName", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type ObjectType = typeof(object);
        private static readonly Type SerializeFieldType = typeof(SerializeField);

        private static readonly Dictionary<int, Item> ProceedObjects = new Dictionary<int, Item>();
        private static readonly HashSet<int> ProceedObjectsWithoutEvents = new HashSet<int>();
        private static int[] _previousSelection;
        private static readonly List<int> Deselected = new List<int>();
        
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
                {
                    item.HierarchyItem.BackgroundColor = color;
                }
            };

            Settings.ValidCallColor.OnChange += color =>
            {
                if (!Settings.IsHighlightingEnabled) return;

                foreach (var item in ProceedObjects.Values.Where(item => item.IsValid))
                {
                    item.HierarchyItem.BackgroundColor = color;
                }
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
            if (ProceedObjects.ContainsKey(instanceId))
            {
                var item = ProceedObjects[instanceId];

                if (Deselected.Contains(instanceId))
                {
                    var go = item.HierarchyItem.GameObject;

                    // If gameObject was destroyed
                    if (go == null) return;

                    var isValidOptional = Validate(go);

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
            {
                if (!Deselected.Remove(instanceId)) return;
            }

            {
                var instance = EditorUtility.InstanceIDToObject(instanceId);

                if (instance == null) return;

                var go = instance as GameObject;

                if (go == null) return;

                var isValidOptional = Validate(go);

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

        private static Optional<bool> Validate(GameObject gameObject)
        {
            var components = gameObject.GetComponents<MonoBehaviour>().Where(c => c != null).ToArray();

            var data = new ObjectData[components.Length];

            for (var i = 0; i < components.Length; i++)
            {
                var monoBehaviour = components[i];
                var type = monoBehaviour.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                data[i] = new ObjectData(monoBehaviour, fields);
            }

            return ValidateUnityEvents(data);
        }

        private static Optional<bool> ValidateUnityEvents(IEnumerable<ObjectData> data)
        {
            var unityEvents = new List<Func<UnityEventBase>>();

            foreach (var objectData in data)
            {
                var monoBehaviour = objectData.MonoBehaviour;
                var fields = objectData.Fields;

                for (var i = 0; i < fields.Length; i++)
                {
                    var fieldInfo = fields[i];

                    if (!BaseUnityEventType.IsAssignableFrom(fieldInfo.FieldType)) continue;
                    if (!fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, SerializeFieldType)) continue;

                    unityEvents.Add(() => (UnityEventBase) fieldInfo.GetValue(monoBehaviour));
                }
            }

            if (unityEvents.Count == 0) 
                return Optional<bool>.FromNone();

            var isValid = true;

            foreach (var @event in unityEvents)
            {
                var e = @event();

                if (e == null) continue;

                var persistentCalls = PersistentCallsField.GetValue(e);
                var count = e.GetPersistentEventCount();

                if (count == 0) continue;

                for (var i = 0; i < count; i++)
                {
                    var call = GetListener.Invoke(persistentCalls, new object[] {i});

                    var mode = (PersistentListenerMode) Mode.GetValue(call);

                    var methodName = e.GetPersistentMethodName(i);
                    var target = e.GetPersistentTarget(i);

                    var arguments = Arguments.GetValue(call);

                    if (arguments == null) continue;

                    var desiredArgTypeName = (string) Argument.GetValue(arguments);
                    var desiredType = ObjectType;

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

        private struct ObjectData
        {
            public MonoBehaviour MonoBehaviour { get; }
            public FieldInfo[] Fields { get; }

            public ObjectData(MonoBehaviour monoBehaviour, FieldInfo[] fields)
            {
                MonoBehaviour = monoBehaviour;
                Fields = fields;
            }
        }

        private class Item
        {
            public HierarchyItem HierarchyItem { get; }
            public bool IsValid { get; }

            public Item(HierarchyItem hierarchyItem, bool isValid)
            {
                HierarchyItem = hierarchyItem;
                IsValid = isValid;
            }
        }
    }
}