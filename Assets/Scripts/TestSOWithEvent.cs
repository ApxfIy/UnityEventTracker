using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "TestSOWithEvent", menuName = "SO/TestSOWithEvent")]
public class TestSOWithEvent : ScriptableObject
{
    public UnityEvent SOEvent;
}
