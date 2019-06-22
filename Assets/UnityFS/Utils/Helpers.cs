using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        public static IEnumerator DestroyAfter(GameObject gameObject, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Object.Destroy(gameObject);
        }

    }
}
