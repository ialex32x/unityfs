using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        public static void GetManifest(IList<string> urls, string localPathRoot, Action<Manifest> callback)
        {
            if (!Directory.Exists(localPathRoot))
            {
                Directory.CreateDirectory(localPathRoot);
            }
            UnityFS.DownloadTask.Create("checksum.txt", null, 4, 0, urls, localPathRoot, 0, checksumTask =>
            {
                var checksum = File.ReadAllText(checksumTask.finalPath);
                Debug.Log($"read checksum {checksum}");
                var manifestPath = Path.Combine(localPathRoot, "manifest.json");
                Manifest manifest = null;
                if (OpenManifestFile(manifestPath, checksum, out manifest))
                {
                    callback(manifest);
                }
                else
                {
                    UnityFS.DownloadTask.Create("manifest.json", checksum, 0, 0, urls, localPathRoot, 0, manifestTask =>
                    {
                        var manifestJson = File.ReadAllText(manifestTask.finalPath);
                        manifest = JsonUtility.FromJson<Manifest>(manifestJson);
                        callback(manifest);
                    }).SetDebugMode(true).Run();
                }
            }).SetDebugMode(true).Run();
        }

        // 打开指定的清单文件 (带校验)
        public static bool OpenManifestFile(string filePath, string checksum, out Manifest manifest)
        {
            if (File.Exists(filePath))
            {
                using (var fs = File.OpenRead(filePath))
                {
                    var crc = new Crc16();
                    crc.Update(fs);
                    if (crc.hex == checksum)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        var bytes = new byte[fs.Position];
                        var read = fs.Read(bytes, 0, bytes.Length);
                        if (read == bytes.Length)
                        {
                            var json = Encoding.UTF8.GetString(bytes);
                            manifest = JsonUtility.FromJson<Manifest>(json);
                            return manifest != null;
                        }
                    }
                }
            }
            manifest = null;
            return false;
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
