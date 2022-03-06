using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEventTracker;
using UnityEventTracker.DataClasses;
using UnityEventTracker.Utils;

public class PersistentCall_Tests
{
    [Test]
    public void Are_Calls_To_The_Same_Method_Different_Event_Names_Only()
    {
        var call_1 = new PersistentCall(
            new Address("5bb62a9fea8abff429e8e9931fe05747", "8405798407779943515"),
            ObjectReference.FromLocal("8477545119671030227", "31e356a211abce146a18acbbaf34bacc", null),
            new ObjectReference(), "SetIdle", (PersistentListenerMode)6, null,
            "OnStartFollow", "bdb99c6481bf24c44be3be41fa0569cb", 893,
            (PersistentCall.PersistentCallState)2);

        var call_2 = new PersistentCall(
            new Address("5bb62a9fea8abff429e8e9931fe05747", "8405798407779943515"),
            ObjectReference.FromLocal("8477545119671030227", "31e356a211abce146a18acbbaf34bacc", null),
            new ObjectReference(), "SetIdle", (PersistentListenerMode)6, null,
            "OnStopFollow", "bdb99c6481bf24c44be3be41fa0569cb", 908,
            (PersistentCall.PersistentCallState)2);

        var areCallToSameMethod = PersistentCallUtils.AreCallToSameMethod(call_1, call_2);

        Assert.IsTrue(areCallToSameMethod);
    }

    [Test]
    public void Serialization_Test()
    {
        var address = new Address("60831707daabd35409bb5314d1598b93", "4454570787811659263");
        var targetInfo = ObjectReference.FromLocal("1586289142248650638", "da5e393edfcb7eb4bb681092b41dc693",
            "Assets.Test.TestClassUsedInEvent, Assembly-CSharp");
        var argumentInfo = new ObjectReference();
        var call = new PersistentCall(address, targetInfo, argumentInfo, "DoStuff", (PersistentListenerMode)1, null,
            "Event", "97437966622eda74a91d29ac6d3f5563", 17, PersistentCall.PersistentCallState.Valid);

        var json = JsonUtility.ToJson(call);

        const string expectedJson =
            "{\"_address\":{\"_assetGuid\":\"60831707daabd35409bb5314d1598b93\",\"_gameObjectId\":\"4454570787811659263\"},\"_targetInfo\":{\"_scriptGuid\":\"da5e393edfcb7eb4bb681092b41dc693\",\"_assemblyTypeName\":\"Assets.Test.TestClassUsedInEvent, Assembly-CSharp\",\"_fileId\":\"1586289142248650638\",\"_assetGuid\":\"\",\"_isLocal\":true},\"_argumentInfo\":{\"_scriptGuid\":\"\",\"_assemblyTypeName\":\"\",\"_fileId\":\"\",\"_assetGuid\":\"\",\"_isLocal\":false},\"_methodName\":\"DoStuff\",\"_listenerMode\":1,\"_argTypes\":[],\"_eventName\":\"Event\",\"_eventScriptGuid\":\"97437966622eda74a91d29ac6d3f5563\",\"_methodLine\":17,\"_state\":0}";

        Assert.AreEqual(expectedJson, json);
    }

    [Test]
    public void Deserialization_Test()
    {
        const string json =
            "{\"_address\":{\"_assetGuid\":\"60831707daabd35409bb5314d1598b93\",\"_gameObjectId\":\"4454570787811659263\"},\"_targetInfo\":{\"_scriptGuid\":\"da5e393edfcb7eb4bb681092b41dc693\",\"_assemblyTypeName\":\"Assets.Test.TestClassUsedInEvent, Assembly-CSharp\",\"_fileId\":\"1586289142248650638\",\"_assetGuid\":\"\",\"_isLocal\":true},\"_argumentInfo\":{\"_scriptGuid\":\"\",\"_assemblyTypeName\":\"\",\"_fileId\":\"\",\"_assetGuid\":\"\",\"_isLocal\":false},\"_methodName\":\"DoStuff\",\"_listenerMode\":1,\"_argTypes\":[],\"_eventName\":\"Event\",\"_eventScriptGuid\":\"97437966622eda74a91d29ac6d3f5563\",\"_methodLine\":17,\"_state\":0}";

        var call = JsonUtility.FromJson<PersistentCall>(json);

        Assert.AreEqual("60831707daabd35409bb5314d1598b93", call.Address.AssetGuid);
        Assert.AreEqual("4454570787811659263", call.Address.GameObjectId);

        Assert.AreEqual("da5e393edfcb7eb4bb681092b41dc693", call.TargetInfo.ScriptGuid);
        Assert.AreEqual("Assets.Test.TestClassUsedInEvent, Assembly-CSharp", call.TargetInfo.AssemblyTypeName);
        var isTargetLocal = call.TargetInfo.IsLocal(out var targetFileId);
        Assert.AreEqual(true, isTargetLocal);
        Assert.AreEqual("1586289142248650638", targetFileId);

        Assert.AreEqual(string.Empty, call.ArgumentInfo.ScriptGuid);
        Assert.AreEqual(string.Empty, call.ArgumentInfo.AssemblyTypeName);
        var isArgLocal = call.ArgumentInfo.IsLocal(out var argFileId);
        Assert.AreEqual(false, isArgLocal);
        Assert.AreEqual(string.Empty, argFileId);

        Assert.AreEqual("DoStuff", call.MethodName);
        Assert.AreEqual(PersistentListenerMode.Void, call.ListenerMode);
        Assert.AreEqual(new int[0], call.ArgTypes);
        Assert.AreEqual("Event", call.EventName);
        Assert.AreEqual("97437966622eda74a91d29ac6d3f5563", call.EventScriptGuid);
        Assert.AreEqual(17, call.MethodLine);
        Assert.AreEqual(PersistentCall.PersistentCallState.Valid, call.State);
    }
}
