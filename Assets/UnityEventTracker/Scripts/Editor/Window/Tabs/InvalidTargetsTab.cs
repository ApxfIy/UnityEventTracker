using System;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.EditorWindow
{
    internal class InvalidTargetsTab : BaseNonMethodsTab
    {
        internal override string Name => "Invalid Targets";

        protected override Func<PersistentCall, bool> GetSelector()
        {
            return c => c.State == PersistentCall.PersistentCallState.InvalidTarget;
        }
    }
}