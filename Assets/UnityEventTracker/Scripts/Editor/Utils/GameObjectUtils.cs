using System.Collections.Generic;
using UnityEngine;

namespace UnityEventTracker.Utils
{
    public static class GameObjectUtils
    {
        public static IEnumerable<GameObject> TraverseRoot(GameObject root)
        {
            yield return root;

            foreach (Transform child in root.transform)
            {
                foreach (var gameObject in TraverseRoot(child.gameObject))
                {
                    yield return gameObject;
                }
            }
        }
    }
}