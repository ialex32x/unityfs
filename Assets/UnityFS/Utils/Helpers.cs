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
        // 基本流程:
        // * 获取远程校验值 checksum.txt
        // * 访问本地 manifest, 对比校验值 checksum
        // * 确定最新 manifest
        // * 创建 BundleAssetProvider
        // * 加载代码包, 产生一个新的 (Zip)FileSystem 传递给脚本引擎 (Exists/ReadAllBytes)
        // * 后续启动流程可由脚本接管
        public static void GetManifest(IList<string> urls, string localPathRoot, Action<Manifest> callback)
        {
            if (!Directory.Exists(localPathRoot))
            {
                Directory.CreateDirectory(localPathRoot);
            }
            UnityFS.DownloadTask.Create("checksum.txt", null, 4, 0, urls, localPathRoot, 0, 10, checksumTask =>
            {
                var checksum = File.ReadAllText(checksumTask.path);
                Debug.Log($"read checksum {checksum}");
                var manifestPath = Path.Combine(localPathRoot, "manifest.json");
                Manifest manifest = null;
                if (OpenManifestFile(manifestPath, checksum, out manifest))
                {
                    callback(manifest);
                }
                else
                {
                    UnityFS.DownloadTask.Create("manifest.json", checksum, 0, 0, urls, localPathRoot, 0, 10, manifestTask =>
                    {
                        var manifestJson = File.ReadAllText(manifestTask.path);
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

        public static Manifest.BundleInfo[] CollectStartupBundles(Manifest manifest, string localPathRoot)
        {
            return CollectBundles(manifest, localPathRoot, info => info.startup);
        }

        public static Manifest.BundleInfo[] CollectBundles(Manifest manifest, string localPathRoot, Func<Manifest.BundleInfo, bool> filter)
        {
            var pending = new List<Manifest.BundleInfo>();
            for (int i = 0, size = manifest.bundles.Count; i < size; i++)
            {
                var bundleInfo = manifest.bundles[i];
                if (filter(bundleInfo))
                {
                    var fullPath = Path.Combine(localPathRoot, bundleInfo.name);
                    if (!IsBundleFileValid(fullPath, bundleInfo))
                    {
                        pending.Add(bundleInfo);
                    }
                }
            }
            return pending.ToArray();
        }

        public static void DownloadBundles(
            string localPathRoot,
            Manifest.BundleInfo[] bundles,
            IList<string> urls,
            StreamingAssetsLoader streamingAssets,
            Action<int, int, ITask> onProgress,
            Action onComplete)
        {
            JobScheduler.DispatchCoroutine(DownloadBundlesCo(localPathRoot, bundles, urls, streamingAssets, onProgress, onComplete));
        }

        // 当前任务数, 总任务数, 当前任务进度
        public static IEnumerator DownloadBundlesCo(
            string localPathRoot,
            Manifest.BundleInfo[] bundles,
            IList<string> urls,
            StreamingAssetsLoader streamingAssets,
            Action<int, int, ITask> onProgress,
            Action onComplete)
        {
            for (int i = 0, size = bundles.Length; i < size; i++)
            {
                var bundleInfo = bundles[i];
                if (streamingAssets != null && streamingAssets.Contains(bundleInfo.name, bundleInfo.checksum, bundleInfo.size))
                {
                    continue;
                }
                var task = DownloadTask.Create(bundleInfo, urls, localPathRoot, -1, 10, null);
                var progress = -1.0f;
                task.SetDebugMode(true).Run();
                while (!task.isDone)
                {
                    if (progress != task.progress)
                    {
                        progress = task.progress;
                        onProgress(i, size, task);
                    }
                    yield return null;
                }
            }
            onComplete();
        }

        // 检查本地 bundle 是否有效
        public static bool IsBundleFileValid(string fullPath, Manifest.BundleInfo bundleInfo)
        {
            try
            {
                var metaPath = fullPath + Metadata.Ext;
                if (File.Exists(fullPath))
                {
                    if (File.Exists(metaPath))
                    {
                        var json = File.ReadAllText(metaPath);
                        var metadata = JsonUtility.FromJson<Metadata>(json);
                        // quick but unsafe
                        if (metadata != null && metadata.checksum == bundleInfo.checksum && metadata.size == bundleInfo.size)
                        {
                            return true;
                        }
                        File.Delete(metaPath);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            return false;
        }

        public static FileStream GetBundleStream(string fullPath, Manifest.BundleInfo bundleInfo)
        {
            try
            {
                if (IsBundleFileValid(fullPath, bundleInfo))
                {
                    var fileStream = System.IO.File.OpenRead(fullPath);
                    return fileStream;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
            return null;
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
