using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class TestClassWithNestedEvent : MonoBehaviour
{
    [field: SerializeField] public Nested1 Instance { get; set; }

    [Serializable]
    public class Nested1
    {
        public Nested2 Nested2Instance;
        public UnityEvent Nested1Event;

        [Serializable]
        public class Nested2
        {
            public Nested2 RecursiveDependency;
            [FormerlySerializedAs("Nested2Event")] public UnityEvent RenamedNested2Event;
        }
    }
}
