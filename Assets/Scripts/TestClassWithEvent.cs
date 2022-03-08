using UnityEngine;
using UnityEngine.Events;

namespace Assets
{
    public class TestClassWithEvent : MonoBehaviour
    {
        public UnityEvent Event;
        public UnityEvent<int> IntEvent;
        
        private void Awake()
        { 
            Event?.Invoke();
        }
    }
}