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
            var rh = new GameObject().AddComponent<ReferenceHolder>();
            rh.target = t;
            return rh;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
#endif
