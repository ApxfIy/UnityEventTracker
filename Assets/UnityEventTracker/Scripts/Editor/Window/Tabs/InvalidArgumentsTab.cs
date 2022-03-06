using System;
using UnityEventTracker.DataClasses;

namespace UnityEventTracker.EditorWindow
{
    internal class InvalidArgumentsTab : BaseNonMethodsTab
    {
        internal override string Name => "Invalid Arguments";

        protected override Func<PersistentCall, bool> GetSelector()
        {
            return (c) => c.State == PersistentCall.PersistentCallState.InvalidArgument;
        }
    }
}