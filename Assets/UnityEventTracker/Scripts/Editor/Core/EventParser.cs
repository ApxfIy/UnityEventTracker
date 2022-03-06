using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.Events;
using UnityEventTracker.DataClasses;
using UnityEventTracker.Utils;

namespace UnityEventTracker
{
    /// <summary>
    /// Looking for <see cref="UnityEvent"/> in <see cref="YAMLObject"/> 
    /// </summary>
    internal ref struct EventParser
    {
        private readonly Asset _asset;
        private readonly Func<string, Optional<YAMLObject>> _getObjectByFileId;
        private readonly Func<string, bool> _doesScriptHasEvents;
        private readonly YAMLObject[] _objects;
        private const string m_Method = "m_Me";
        private const string m_Mode = "m_Mo";
        private const string m_Target = "m_Target";
        private const string m_Calls = "m_Calls";
        private const string m_PersistentCalls = "m_PersistentCalls";

        public EventParser(Asset asset, Func<string, bool> doesClassHasEvents)
        {
            _asset = asset;
            _doesScriptHasEvents = doesClassHasEvents;
            _objects = _asset.GetObjects().ToArray();

            var objectsMapping = _objects.ToDictionary(o => o.FileId, o => o);
            _getObjectByFileId = (s) =>
                objectsMapping.ContainsKey(s)
                    ? Optional<YAMLObject>.FromSome(objectsMapping[s])
                    : Optional<YAMLObject>.FromNone();
        }

        public IEnumerable<PersistentCall> Parse()
        {
            var result = new List<PersistentCall>();

            foreach (var yamlObject in _objects)
            {
                try
                {
                    if (FindEvents(yamlObject).HasValue(out var events))
                    {
                        result.AddRange(events);
                    }
                }
                catch (Exception e)
                {
                    Logger.CreateBugReport(Path.GetFileNameWithoutExtension(_asset.RelativePath),
                        File.ReadAllText(_asset.AbsolutePath), e);
                }
            }

            return result;
        }

        private Optional<List<PersistentCall>> FindEvents(YAMLObject yamlObject)
        {
            if (!yamlObject.IsMonoBehaviour(out var scriptGuid, out var gameObjectId))
                return Optional<List<PersistentCall>>.FromNone();

            if (!_doesScriptHasEvents(scriptGuid))
                return Optional<List<PersistentCall>>.FromNone();

            var calls = new List<PersistentCall>();
            var content = yamlObject.Content;

            for (var i = 8; i < content.Count - 1; i++)
            {
                var line = content[i];

                // Skipping nested events for now
                if (line.Length < 5 || line[4] == ' ')
                    continue;

                if (!line.TrimStart(' ').StartsWith(m_PersistentCalls))
                    continue;

                i++;

                if (i >= content.Count)
                    continue;

                var callsLine = content[i];

                if (!callsLine.TrimStart(' ').StartsWith(m_Calls))
                    continue;

                if (callsLine.EndsWith(']'))
                    continue; // m_Calls is empty list

                var eventLine = i - 2;
                var savedEventName = content[eventLine]
                    .Substring(0, content[eventLine].Length - 1)
                    .TrimStart(' ');

                var eventScriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                var eventScriptType = AssetDatabase.LoadAssetAtPath<MonoScript>(eventScriptPath).GetClass();

                // Verify that event still exists and it's not a leftover calls
                // TODO Maybe I need to inform user about this calls anyway?
                if (!TypeUtils.GetSerializedField(eventScriptType, savedEventName).HasValue(out var eventInfo))
                    continue;

                var actualEventName = eventInfo.Name;
                var eventType = eventInfo.FieldType;

                var address = new Address(_asset.Guid, gameObjectId);

                i++; // points to - m_Target line

                do
                {
                    var targetReference = TryGetObjectReference(content, ref i, _getObjectByFileId);
                    var callState = PersistentCall.PersistentCallState.Valid;

                    // Target doesn't exist
                    if (!targetReference.HasValue(out var target))
                    {
                        callState = PersistentCall.PersistentCallState.InvalidTarget;
                    }

                    var methodLineIndex = i + yamlObject.StartingLineIndex;
                    var methodName = GetMethodName(content, ref i);
                    var argType = GetArgumentType(content, ref i);

                    var isMethodValid = PersistentCallUtils.ValidateMethodCall(target, methodName, argType,
                        actualEventName, scriptGuid);

                    // Method isn't valid
                    if (!isMethodValid)
                    {
                        callState = callState == PersistentCall.PersistentCallState.Valid
                            ? PersistentCall.PersistentCallState.InvalidMethod
                            : callState;
                    }

                    string[] argTypes = null;

                    if (argType == PersistentListenerMode.EventDefined)
                    {
                        argTypes = eventType.GenericTypeArguments.Select(t => t.AssemblyQualifiedName).ToArray();
                    }

                    i++;

                    var objectArgReference = TryGetObjectReference(content, ref i, _getObjectByFileId);
                    
                    // Object argument isn't valid
                    if (!objectArgReference.HasValue(out var objectArg) && argType == PersistentListenerMode.Object)
                    {
                        callState = callState == PersistentCall.PersistentCallState.Valid
                            ? PersistentCall.PersistentCallState.InvalidArgument
                            : callState;
                    }
                    else if (argType == PersistentListenerMode.Object && !objectArg.IsUnityType())
                    {
                        var objectArgSavedType = Type.GetType(objectArg.AssemblyTypeName);

                        if (objectArgSavedType == null
                        || ScriptAsset.FromGuid(objectArg.ScriptGuid).HasValue(out var objectArgActualClass) 
                        && objectArgSavedType != objectArgActualClass.Type)
                        {
                            callState = callState == PersistentCall.PersistentCallState.Valid
                                ? PersistentCall.PersistentCallState.InvalidArgument
                                : callState;
                        }
                    }

                    var persistentCall =
                        new PersistentCall(address, target, objectArg, methodName, argType, argTypes, actualEventName,
                            scriptGuid, methodLineIndex, callState);

                    calls.Add(persistentCall);

                    i += 5; // this should point to the line after m_CallState line
                }
                while (i < content.Count && content[i].Contains(m_Target));
            }

            return Optional<List<PersistentCall>>.FromSome(calls);
        }

        private static Optional<ObjectReference> TryGetObjectReference(IReadOnlyList<string> content, ref int fileIdLineIndex, Func<string, Optional<YAMLObject>> getObjectByFileId)
        {
            // Real world examples
            {
                // 1) TODO
                //  OnPlay:
                //      m_PersistentCalls:
                //          m_Calls:
                //              -m_Target: { fileID: 4955299949663416073}
                //              m_MethodName: SetIdle
                //              m_Mode: 6
                //              m_Arguments:
                //                  m_ObjectArgument: { fileID: 0}
                //                  m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
                //                  m_IntArgument: 0
                //                  m_FloatArgument: 0
                //                  m_StringArgument:
                //                  m_BoolArgument: 0
                //              m_CallState: 2
                // The problem with this call is that it doesn't contain
                // m_TargetAssemblyTypeName line so GetMethodName will throw IndexOutOfRangeException because var line wil be
                // equal to "m_Mode: 6". Another note is this this call doesn't exist anymore (event was renamed and calls list is clear)
                // so, what to do? Maybe I should check previous line if I facing this Exception?

                // 2) TODO There cloud be a line break
                //  <OnPanelChangeStart> k__BackingField:
                //      m_PersistentCalls:
                //          m_Calls:
                //              -m_Target: { fileID: 900048125}
                //              m_TargetAssemblyTypeName: Assets.Scripts.Scenes.Scene_Main.EasterEggGameHint,
                //                  Assembly - CSharp
                //              m_MethodName: StopHintAnimation
                //              m_Mode: 1
                //              m_Arguments:
                //                  m_ObjectArgument: { fileID: 0}
                //                  m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
                //                  m_IntArgument: 0
                //                  m_FloatArgument: 0
                //                  m_StringArgument:
                //                  m_BoolArgument: 0
                //              m_CallState: 2

                // 3) TODO
                //  SomeEvent:
                //      m_PersistentCalls:
                //          m_Calls:
                //              -m_Target: { fileID: 570827283}
                //              m_TargetAssemblyTypeName: Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Long.Namespace.Some,
                //                  Assembly - CSharp
                //              m_MethodName: MethodThatTakesComponent
                //              m_Mode: 2
                //              m_Arguments:
                //                  m_ObjectArgument: { fileID: 570827283}
                //                  m_ObjectArgumentAssemblyTypeName: Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Very.Long.Namespace.Some,
                //                      Assembly - CSharp
                //                  m_IntArgument: 0
                //                  m_FloatArgument: 0
                //                  m_StringArgument:
                //                  m_BoolArgument: 0
                //              m_CallState: 2
            }
            var fileIdLine = content[fileIdLineIndex];
            fileIdLineIndex++; // points to m_TargetAssemblyTypeName or m_MethodName

            var assemblyTypeName = string.Empty;
            const string assemblyTypeNameMarker = "Assembly";
            var assemblyTypeNameLine = content[fileIdLineIndex];

            var startIndex = assemblyTypeNameLine.IndexOf(assemblyTypeNameMarker, StringComparison.InvariantCulture);

            if (startIndex >= 0)
            {
                fileIdLineIndex++; // points to second line of m_TargetAssemblyTypeName or m_MethodName

                const int m_TargetAssemblyTypeNameLength = 18;
                assemblyTypeName = assemblyTypeNameLine.Substring(startIndex + m_TargetAssemblyTypeNameLength);

                if (assemblyTypeName.EndsWith(','))
                {
                    var assemblyLine = content[fileIdLineIndex];

                    assemblyTypeName += " " + assemblyLine.TrimStart(' ');

                    fileIdLineIndex++; // points to m_MethodName
                }
            }
            
            var indexOfGuidD = fileIdLine.IndexOf("guid", StringComparison.InvariantCulture);

            //-m_Target: { fileID: 11400000, guid: 93c88ad75feeefb4ebd9b3658be82794, type: 2} this is how this line looks like if the target is SO
            //-m_Target: { fileID: 1586289142248650638} 

            if (indexOfGuidD >= 0)
            {
                const int guidValueLength = 32;
                const int guidLength = 6;

                // Target is asset, we can get its guid directly from this line
                var startOffset = indexOfGuidD + guidLength;
                var assetGuid = fileIdLine.Substring(startOffset, guidValueLength);
                var optionalAsset = Asset.FromGuid(assetGuid);

                if (optionalAsset.HasValue(out var asset))
                {
                    var objects = asset.GetObjects().ToArray(); // It's length is supposed to be equal to 1

                    // It's some type of SO
                    if (objects.Length == 1 && objects[0].IsMonoBehaviour(out var scriptGuid, out var gameObjectId))
                        return Optional<ObjectReference>.FromSome(ObjectReference.FromGlobal(assetGuid, scriptGuid, assemblyTypeName));

                    // It's non-script asset (audio, sprite, etc.)
                    return Optional<ObjectReference>.FromSome(ObjectReference.FromGlobal(assetGuid, null, assemblyTypeName));
                }

                return Optional<ObjectReference>.FromNone();
            }
            else
            {
                const int fileIdLength = 8;
                var indexOfFileId = fileIdLine.IndexOf("fileID", StringComparison.InvariantCulture);
                var offset = indexOfFileId + fileIdLength;

                // Target is local component or GameObject
                var fileId = fileIdLine.Substring(offset, fileIdLine.Length - offset - 1);

                if (getObjectByFileId(fileId).HasValue(out var target))
                {
                    target.IsMonoBehaviour(out var guid, out var gameObjectId);
                    return Optional<ObjectReference>.FromSome(ObjectReference.FromLocal(fileId, guid, assemblyTypeName));
                }

                return Optional<ObjectReference>.FromNone();
            }
        }

        private static string GetMethodName(IReadOnlyList<string> content, ref int methodLineIndex)
        {
            const int m_MethodNameLength = 14;

            var line = content[methodLineIndex];
            var startIndex = line.IndexOf(m_Method, StringComparison.InvariantCulture);

            methodLineIndex++;

            return line.Substring(startIndex + m_MethodNameLength);
        }

        private static PersistentListenerMode GetArgumentType(IReadOnlyList<string> content, ref int modeLineIndex)
        {
            const int m_ModeLength = 8;

            var line = content[modeLineIndex];
            var offset = line.IndexOf(m_Mode, StringComparison.InvariantCulture);
            var mode = line.Substring(offset + m_ModeLength);

            modeLineIndex++;

            return (PersistentListenerMode)int.Parse(mode);
        }
    }
}