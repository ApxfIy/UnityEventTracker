using System.Reflection;

namespace UnityEventTracker.Utils
{
    public static class PrefabStageExtensions
    {
        public static bool SaveUsingReflection(this UnityEditor.SceneManagement.PrefabStage prefabStage)
        {
            var type   = prefabStage.GetType();
            var method = type.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (bool) method.Invoke(prefabStage, null);
            return result;
        }
    }
}
