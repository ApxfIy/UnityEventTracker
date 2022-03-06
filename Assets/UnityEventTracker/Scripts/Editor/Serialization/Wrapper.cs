using System;

namespace UnityEventTracker.Serialization
{
    [Serializable]
    internal class Wrapper<T>
    {
        public T Data;

        public Wrapper(T data)
        {
            Data = data;
        }
    }
}