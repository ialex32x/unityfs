using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        // public static void CopyStream()
        
        public static IEnumerator DestroyAfter(GameObject gameObject, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Object.Destroy(gameObject);
        }

        public static IEnumerator InvokeAfter(Action action, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }
    }
}
