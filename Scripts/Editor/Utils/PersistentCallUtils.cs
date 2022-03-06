using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEventTracker.DataClasses;
using Object = UnityEngine.Object;

namespace UnityEventTracker.Utils
{
    internal static class PersistentCallUtils
    {
        internal static void ReplaceAllMethods(IEnumerable<PersistentCall> calls, string newMethodName)
        {
            var cachedData = new Dictionary<Asset, string[]>();

            foreach (var call in calls)
            {
                var isValid = PersistentCallUtils.ValidateMethodCall(call.TargetInfo, newMethodName, call.ListenerMode,
                    call.EventName, call.EventScriptGuid);

                if (!isValid)
                {
                    Debug.LogError($"Was about to replace {call.MethodName} with {newMethodName}, but new method call is still invalid!");
                    continue;
                }

                var line  = call.MethodLine;
                var asset = Asset.FromGuid(call.Address.AssetGuid).GetValueUnsafe();

                string[] data;

                if (cachedData.ContainsKey(asset))
                    data = cachedData[asset];
                else
                {
                    data = File.ReadAllLines(asset.AbsolutePath);
                    cachedData.Add(asset, data);
                }

                data[line] = data[line].Replace(call.MethodName, newMethodName);
            }

            // TODO add logic to skip check for all modified assets, because at this point all call
            // are verified, but File.WriteAllLines will trigger AssetPostprocessorEvents.OnAssetsImported  
            foreach (var (asset, text) in cachedData)
            {
                File.WriteAllLines(asset.AbsolutePath, text);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUncompressedImport);
        }

        internal static bool ValidateMethodCall(
            ObjectReference target,
            string methodName,
            PersistentListenerMode listenerMode,
            string eventName,
            string eventScriptGuid)
        {
            // This means that we call method of native Unity component like GameObject, Transform etc.
            if (target.IsUnityType())
                return true;

            var scriptGuid = target.ScriptGuid;

            if (!ScriptAsset.FromGuid(scriptGuid).HasValue(out var scriptAsset))
                return false;

            if (listenerMode != PersistentListenerMode.EventDefined && listenerMode != PersistentListenerMode.Object)
            {
                var methodInfo = listenerMode == PersistentListenerMode.Void
                    ? scriptAsset.Type.GetMethod(methodName, new Type[0])
                    : scriptAsset.Type.GetMethod(methodName,
                        new[] { TypeUtils.PersistentListenerModeToType(listenerMode) });

                if (methodInfo == null) // Method was deleted/moved. Must inform the user
                {
                    Debug.LogWarning($"{methodName} doesn't exist");
                    return false;
                }

                if (methodInfo.ReturnType != typeof(void))
                {
                    // TODO
                    Debug.LogWarning($"{methodName} return type is not void");
                    return false;
                }

                if (methodInfo.IsGenericMethod)
                {
                    Debug.LogWarning($"{methodName} can't be generic");
                    return false;
                }

                // If we are here method is valid (or I forgot some checks)

                return true;
            }
            else if (listenerMode == PersistentListenerMode.Object)
            {
                var methods = scriptAsset.Type.GetMethods()
                    .Where(m => m.Name == methodName)
                    .Where(m => m.GetParameters().Length == 1)
                    .Where(m => !m.IsGenericMethod)
                    .ToArray();

                if (methods.Length == 0) // Method was deleted/moved. Must inform the user
                {
                    Debug.LogWarning($"{methodName} doesn't exist");
                    return false;
                }

                foreach (var methodInfo in methods)
                {
                    var methodParameters = methodInfo.GetParameters();
                    var paramatersMatch = methodParameters.All(parameter => typeof(Object).IsAssignableFrom(parameter.ParameterType));

                    if (!paramatersMatch) continue;

                    return true;
                }

                Debug.LogWarning($"{methodName} parameter mismatch");
                return false;
            }
            else
            {
                var methods = scriptAsset.Type.GetMethods()
                    .Where(m => m.Name == methodName)
                    .Where(m => m.ReturnType == typeof(void))
                    .ToArray();

                if (methods.Length == 0) // Method was deleted/moved. Must inform the user
                {
                    Debug.LogWarning($"{methodName} doesn't exist");
                    return false;
                }

                var eventScriptPath = AssetDatabase.GUIDToAssetPath(eventScriptGuid);
                var eventScriptType = AssetDatabase.LoadAssetAtPath<MonoScript>(eventScriptPath).GetClass();

                Type eventType = null;
                var currentEventName = eventName;

                TypeUtils.GetSerializedField(eventScriptType, eventName)
                    .OnSome(f =>
                    {
                        eventType = f.FieldType;
                        currentEventName = f.Name;
                    });

                if (eventType == null)
                {
                    // Event was removed or renamed without usage of [FormerlySerializedAsAttribute] attribute
                    // TODO
                    Debug.LogWarning($"Can't find event {eventName} in {eventScriptType}");
                    return false;
                }

                if (eventName != currentEventName)
                {
                    // Event was renamed. All MethodCalls where EventScriptGuid == usedMethod.EventScriptGuid
                    // && EventName == usedMethod.EventName in MethodsUsedInEventsContainer must be updated
                    Debug.LogWarning($"Event was renamed from {eventName} to {currentEventName} in {eventScriptType}");
                }

                foreach (var methodInfo in methods)
                {
                    var methodParameters = methodInfo.GetParameters();
                    var eventArgTypes = GetDelegateArgumentsTypes(eventType);
                    var paramatersMatch = true;

                    if (methodParameters.Length != eventArgTypes.Count)
                    {
                        Debug.LogWarning("Parameters length is not equal to event args length. \n" +
                                         $"Method {methodName} in {scriptAsset.Type} \n" +
                                         $"Event {eventName} in {eventScriptType}");
                        continue;
                    }

                    for (var i = 0; i < eventArgTypes.Count; i++)
                    {
                        if (eventArgTypes[i].IsAssignableFrom(methodParameters[i].ParameterType))
                            continue;

                        Debug.LogWarning($"{methodParameters[i].ParameterType} in not assignable from {eventArgTypes[i]}");
                        paramatersMatch = false;
                        break;
                    }

                    if (!paramatersMatch) continue;

                    // If we are here method is valid (or I forgot some checks)
                    return true;
                }

                Debug.LogWarning($"{methodName} parameter mismatch");
                return false;
            }
        }

        internal static bool ValidateMethodCall(PersistentCall persistentCall)
        {
            return ValidateMethodCall(persistentCall.TargetInfo, persistentCall.MethodName,
                persistentCall.ListenerMode, persistentCall.EventName, persistentCall.EventScriptGuid);
        }

        internal static bool AreCallToSameMethod(PersistentCall first, PersistentCall second)
        {
            if (first == null && second == null)
                return false;

            if (first == null)
                return false;

            if (second == null)
                return false;

            if (ReferenceEquals(first, second))
                return true;

            if (first.ListenerMode != second.ListenerMode) // TODO I separating dynamic and static modes, maybe I shouldn't?
                return false;

            if (first.TargetInfo.ScriptGuid != second.TargetInfo.ScriptGuid)
                return false;

            if (first.MethodName != second.MethodName)
                return false;

            if (first.ListenerMode != PersistentListenerMode.EventDefined)
                return true;

            if (first.ArgTypes.Length != second.ArgTypes.Length)
                return false;

            for (var i = 0; i < first.ArgTypes.Length; i++)
            {
                if (first.ArgTypes[i] == second.ArgTypes[i]) continue;

                return false;
            }

            return true;
        }

        internal static MethodInfo[] GetPossibleDynamicMethods(PersistentCall call)
        {
            if (call.ListenerMode == PersistentListenerMode.Void)
                return new MethodInfo[0];

            var eventScriptGuid = call.EventScriptGuid;
            var eventScriptPath = AssetDatabase.GUIDToAssetPath(eventScriptGuid);
            var eventScriptType = AssetDatabase.LoadAssetAtPath<MonoScript>(eventScriptPath).GetClass();
            
            if (!TypeUtils.GetSerializedField(eventScriptType, call.EventName).HasValue(out var eventFieldInfo))
                return new MethodInfo[0];

            var eventType = eventFieldInfo.FieldType;
            var targetTypeOptional = call.GetTargetType();

            return targetTypeOptional.HasValue(out var targetType) ? GetPossibleDynamicMethods(eventType, targetType) : new MethodInfo[0];
        }

        internal static MethodInfo[] GetPossibleStaticMethods(PersistentCall call)
        {
            var targetTypeOptional = call.GetTargetType();

            if (!targetTypeOptional.HasValue(out var targetType))
                return new MethodInfo[0];

            var methods =
                CalculateMethodMap(targetType, new Type[] { }, false)
                    .Concat(CalculateMethodMap(targetType, new[] {typeof(float)}, false))
                    .Concat(CalculateMethodMap(targetType, new[] {typeof(int)}, false))
                    .Concat(CalculateMethodMap(targetType, new[] {typeof(string)}, false))
                    .Concat(CalculateMethodMap(targetType, new[] {typeof(bool)}, false))
                    .Concat(CalculateMethodMap(targetType, new[] {typeof(Object)}, true));
                    
            return methods.ToArray();
        }
        
        private static IReadOnlyList<Type> GetDelegateArgumentsTypes(Type eventType)
        {
            var delegateMethod = eventType.GetMethod("Invoke");
            return delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();
        }

        private static IEnumerable<MethodInfo> GetPossibleStaticMethods(Type targetType, PersistentListenerMode mode)
        {
            var voidMethods = CalculateMethodMap(targetType, new Type[] { }, false);

            if (mode == PersistentListenerMode.Void)
                return voidMethods;

            var type = TypeUtils.PersistentListenerModeToType(mode);
            var additionalMethods = CalculateMethodMap(targetType, new[] { type }, mode == PersistentListenerMode.Object);

            return voidMethods.Concat(additionalMethods);
        }

        private static MethodInfo[] GetPossibleDynamicMethods(Type eventType, Type targetType)
        {
            var delegateArgumentsTypes = GetDelegateArgumentsTypes(eventType);
            return CalculateMethodMap(targetType, delegateArgumentsTypes, false).ToArray();
        }

        private static IEnumerable<MethodInfo> CalculateMethodMap(Type targetType, IReadOnlyList<Type> types, bool allowSubclasses)
        {
            if (types == null)
                yield break;

            // find the methods on the behaviour that match the signature
            var componentType = targetType;
            var componentMethods = componentType.GetMethods().Where(x => !x.IsSpecialName).ToList();

            var wantedProperties = componentType.GetProperties().AsEnumerable();
            wantedProperties = wantedProperties.Where(x =>
                x.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length == 0 && x.GetSetMethod() != null);
            componentMethods.AddRange(wantedProperties.Select(x => x.GetSetMethod()));

            foreach (var componentMethod in componentMethods)
            {
                // if the argument length is not the same, no match
                var componentParamaters = componentMethod.GetParameters();
                if (componentParamaters.Length != types.Count)
                    continue;

                // Don't show obsolete methods.
                if (componentMethod.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0)
                    continue;

                if (componentMethod.ReturnType != typeof(void))
                    continue;
                
                // if the argument types do not match, no match
                var parametersMatch = true;
                for (var i = 0; i < types.Count; i++)
                {
                    if (!componentParamaters[i].ParameterType.IsAssignableFrom(types[i]))
                        parametersMatch = false;

                    if (allowSubclasses && types[i].IsAssignableFrom(componentParamaters[i].ParameterType))
                        parametersMatch = true;
                }

                if (!parametersMatch) continue;

                yield return componentMethod;
            }
        }
    }
}