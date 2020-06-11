#if UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityFS
{
    public class ReferenceHolder : MonoBehaviour, IDisposable
    {
        public Object target;

        public static ReferenceHolder Create(Object t)
        {
            var go = new GameObject();
            var rh = go.AddComponent<ReferenceHolder>();
            rh.target = t;
            go.hideFlags = HideFlags.HideAndDontSave;
            return rh;
        }

        public void Dispose()
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
#endif
