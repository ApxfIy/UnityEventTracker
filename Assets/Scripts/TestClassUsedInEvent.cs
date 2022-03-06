using UnityEngine;

namespace Assets.Test
{
    public class TestClassUsedInEvent : MonoBehaviour
    {
        public int SomeField;

        public void DoStuff2()
        {
              
        }

        public void SomeOtherMethod()  
        {
            Debug.Log("Here");   
        }

        private void PrivateMethod()
        {

        }

        public void MethodWithIntParameter(int a)
        {
                  
        }

        public void MethodWithIntParameter2(int a)
        {

        }

        public void TestDynamicMethod(InnerClass innerClass)
        {

        }

        public void TestDynamicMethod2(InnerClass innerClass, int a)
        {

        }

        public void MethodThatTakesObject(Object o)
        {
            Debug.Log(o.name);
        }
        
        //public void MethodThatTakesObject(GameObject o)
        //{

        //}

        public class InnerClass
        {
            
        }

        public class InnerClass2 : InnerClass
        {
            
        }
    }

    public class SecondClassInFile
    {

    }
}