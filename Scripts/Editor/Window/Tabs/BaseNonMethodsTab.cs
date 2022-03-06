using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.EditorWindow
{
    internal abstract class BaseNonMethodsTab : BaseTab
    {
        internal override void OnEnable()
        {
            base.OnEnable();

            Initialize();
        }

        internal override void Draw()
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawAssets(Rect.width);
                }
            }
        }

        protected abstract Func<PersistentCall, bool> GetSelector();

        protected override void OnDataChanged()
        {
            Initialize();
        }

        private void Initialize()
        {
            var calls = UnityEventTracker.CallsContainer.GetAllCalls();

            var selector = GetSelector();

            var selectedCalls = calls.Where(selector).ToArray();

            var addresses = selectedCalls.Select(c => c.Address);

            UpdateCurrentAssetsArray(addresses);
            UpdateActiveAssetAndRelatedData();
        }
    }
}