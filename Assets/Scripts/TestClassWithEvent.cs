using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets
{
    public class TestClassWithEvent : MonoBehaviour
    {
        public UnityEvent Event;
        public UnityEvent<int> IntEvent;
        
        [SerializeField] private Nested1 Nested1Instance;

        private void Awake()
        { 
            Event?.Invoke();
        }

        [Serializable]
        private class Nested1
        {
            public UnityEvent Nested1Event;
            public Nested2 Nested2Instance;

            [Serializable]
            public class Nested2
            {
                public UnityEvent Nested2Event;
            }
        }
    }
}