using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        public static IList<string> URLs(params string[] urls)
        {
            return new List<string>(urls);
        }

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
