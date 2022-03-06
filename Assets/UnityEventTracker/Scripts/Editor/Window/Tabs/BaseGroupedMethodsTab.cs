using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEventTracker.DataClasses;
using UnityEventTracker.Utils;
using Object = UnityEngine.Object;

namespace UnityEventTracker.EditorWindow
{
    internal abstract class BaseGroupedMethodsTab : BaseTab
    {
        private        int                    _selectedMethodIndex = 0;
        private static List<PersistentCall>[] _groupedCalls;
        private static string[]               _methodNames;

        internal override void OnEnable()
        {
            base.OnEnable();

            UpdateMethodsList();
            OnDataChanged();
        }

        internal override void Draw()
        {
            using (new GUILayout.HorizontalScope())
            {
                if (_groupedCalls?.Length == 0)
                    return;

                using (GetVerticalGroup())
                    DrawGroupedMethods();

                using (GetVerticalGroup())
                    DrawAssets(Rect.width / 3f);

                using (GetVerticalGroup())
                    DrawMethodCandidates();
            }
        }

        protected abstract bool ShouldProceedCall(PersistentCall call);

        protected abstract bool ShouldGroupCalls(PersistentCall first, PersistentCall second);

        protected override void OnDataChanged()
        {
            UpdateMethodsList();

            if (_groupedCalls.Length == 0)
                return;

            _selectedMethodIndex = Math.Clamp(_selectedMethodIndex, 0, _groupedCalls.Length - 1);

            OnMethodSelected();
        }

        private IEnumerable<List<PersistentCall>> GroupMethods(IReadOnlyList<PersistentCall> calls)
        {
            var checkedIndices = new HashSet<int>();

            for (var i = 0; i < calls.Count; i++)
            {
                if (checkedIndices.Contains(i)) continue;

                var persistentCall = calls[i];

                if (!ShouldProceedCall(persistentCall)) continue;

                var group = new List<PersistentCall> {persistentCall};
                checkedIndices.Add(i);

                for (var j = i + 1; j < calls.Count; j++)
                {
                    var call = calls[j];

                    if (!ShouldProceedCall(call)) continue;

                    if (!ShouldGroupCalls(persistentCall, call)) continue;

                    group.Add(call);
                    checkedIndices.Add(j);
                }

                yield return group;
            }
        }

        private void OnMethodSelected()
        {
            var addresses = _groupedCalls[_selectedMethodIndex].Select(c => c.Address);

            UpdateCurrentAssetsArray(addresses);
            UpdateActiveAssetAndRelatedData();
        }

        private void UpdateMethodsList()
        {
            var calls = UnityEventTracker.CallsContainer.GetAllCalls();

            _groupedCalls = GroupMethods(calls).ToArray();
            _methodNames = _groupedCalls.Select(p =>
            {
                var call = p[0];

                if (call.TargetInfo.IsUnityType())
                    return $"{call.MethodName}";

                var script     = ScriptAsset.FromGuid(call.TargetInfo.ScriptGuid).GetValueUnsafe();
                var scriptName = script.Script.name;
                return $"{scriptName}.{call.MethodName}";
            }).ToArray();
        }

        private Vector2 _methodsScrollPosition;

        private void DrawGroupedMethods()
        {
            _methodsScrollPosition = GUILayout.BeginScrollView(_methodsScrollPosition);

            // TODO show more info about method (script it belongs to and its path)
            _selectedMethodIndex = GUILayout.SelectionGrid(_selectedMethodIndex, _methodNames, 1);

            if (GUI.changed)
            {
                var isValidMethod = _selectedMethodIndex >= 0 && _selectedMethodIndex < _groupedCalls.Length;

                if (isValidMethod)
                    OnMethodSelected();
            }

            GUILayout.EndScrollView();
        }

        private int          _selectedDynamicMethodToReplace = -1;
        private int          _selectedStaticMethodToReplace  = -1;
        private MethodInfo[] _dynamicMethodCandidates;
        private MethodInfo[] _staticMethodCandidates;
        private int          _previouslySelectedMethodIndex = -1;
        private Vector2      _methodsCandidatesScrollPosition;
        private bool         _showAllMethods;
        private bool         _shouldUpdateMethodsCandidatesList;

        private static readonly Type[] BaseUnityTypes =
        {
            typeof(Object),
            typeof(GameObject),
            typeof(Component),
            typeof(MonoBehaviour),
            typeof(ScriptableObject)
        };

        private void DrawMethodCandidates()
        {
            var currentCall = _groupedCalls[_selectedMethodIndex][0];

            if (_previouslySelectedMethodIndex != _selectedMethodIndex || _shouldUpdateMethodsCandidatesList)
            {
                var staticMethods  = PersistentCallUtils.GetPossibleStaticMethods(currentCall);
                var dynamicMethods = PersistentCallUtils.GetPossibleDynamicMethods(currentCall);

                if (_showAllMethods)
                {
                    _dynamicMethodCandidates = dynamicMethods;
                    _staticMethodCandidates  = staticMethods;
                }
                else
                {
                    _dynamicMethodCandidates =
                        dynamicMethods.Where(m => !BaseUnityTypes.Contains(m.DeclaringType)).ToArray();
                    _staticMethodCandidates =
                        staticMethods.Where(m => !BaseUnityTypes.Contains(m.DeclaringType)).ToArray();
                }

                _previouslySelectedMethodIndex     = _selectedMethodIndex;
                _shouldUpdateMethodsCandidatesList = false;
            }

            _methodsCandidatesScrollPosition = GUILayout.BeginScrollView(_methodsCandidatesScrollPosition);

            // Dynamic methods

            GUILayout.Label("Dynamic methods", EditorStyles.label);

            _selectedDynamicMethodToReplace = GUILayout.SelectionGrid(_selectedDynamicMethodToReplace,
                _dynamicMethodCandidates
                    .Select(m => m.Name).ToArray(), 1);

            if (GUI.changed)
                _selectedStaticMethodToReplace = -1;

            GUILayout.Space(10);

            // Static methods

            GUILayout.Label("Static methods", EditorStyles.label);

            _selectedStaticMethodToReplace = GUILayout.SelectionGrid(_selectedStaticMethodToReplace,
                _staticMethodCandidates
                    .Select(m => m.Name).ToArray(), 1);

            if (GUI.changed)
                _selectedDynamicMethodToReplace = -1;

            GUILayout.Space(10);

            _showAllMethods = GUILayout.Toggle(_showAllMethods, "Show all methods", EditorStyles.toggle);

            if (GUI.changed)
                _shouldUpdateMethodsCandidatesList = true;

            GUILayout.Space(10);
            using (new EditorGUI.DisabledGroupScope(_selectedStaticMethodToReplace < 0 &&
                                                    _selectedDynamicMethodToReplace < 0))
            {
                if (GUILayout.Button("Replace"))
                {
                    var isSaved =
                        AssetUtils.AskUserToSaveModifiedAssets(2,
                            "Modified assets and scenes must be saved");

                    if (!isSaved) return;

                    var methodName = _selectedStaticMethodToReplace > -1
                        ? _staticMethodCandidates[_selectedStaticMethodToReplace].Name
                        : _dynamicMethodCandidates[_selectedDynamicMethodToReplace].Name;
                    PersistentCallUtils.ReplaceAllMethods(_groupedCalls[_selectedMethodIndex], methodName);
                }
            }

            GUILayout.EndScrollView();
        }

        private IDisposable GetVerticalGroup()
        {
            var width = Rect.width / 3f;
            return new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width));
        }
    }
}