using UnityEventTracker.DataClasses;
using UnityEventTracker.Utils;

namespace UnityEventTracker.EditorWindow
{
    internal class ValidCallsTab : BaseGroupedMethodsTab
    {
        internal override string Name => "Valid Calls";

        protected override bool ShouldGroupCalls(PersistentCall first, PersistentCall second)
        {
            return PersistentCallUtils.AreCallToSameMethod(first, second);
        }

        protected override bool ShouldProceedCall(PersistentCall call)
        {
            return call.State == PersistentCall.PersistentCallState.Valid;
        }
    }
}