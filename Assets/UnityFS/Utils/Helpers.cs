using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        public static bool OpenManifest(string filePath, string checksum, out Manifest manifest)
        {
            manifest = null;
            return false;
            // var fullPath = Path.Combine(_localPathRoot, bundle.name);
            // var metaPath = fullPath + Metadata.Ext;
            // if (File.Exists(fullPath))
            // {
            //     if (File.Exists(metaPath))
            //     {
            //         var json = File.ReadAllText(metaPath);
            //         var metadata = JsonUtility.FromJson<Metadata>(json);
            //         // quick but unsafe
            //         if (metadata.checksum == bundle.checksum && metadata.size == bundle.size)
            //         {
            //             var fileStream = System.IO.File.OpenRead(fullPath);
            //             bundle.Load(fileStream); // 生命周期转由 UAssetBundleBundle 管理
            //             return true;
            //         }
            //         File.Delete(metaPath);
            //     }
            // }
            // return false;
        }

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
