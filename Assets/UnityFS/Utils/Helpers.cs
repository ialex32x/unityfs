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
        // 在不知道清单文件校验值和大小的情况下, 使用此接口尝试先下载 checksum 文件, 得到清单文件信息
        public static void GetManifest(string localPathRoot, string checksum, int size, Action<Manifest> callback)
        {
            if (checksum != null && size != 0)
            {
                GetManifestDirect(localPathRoot, checksum, size, true, callback);
                return;
            }
            if (!Directory.Exists(localPathRoot))
            {
                Directory.CreateDirectory(localPathRoot);
            }
            var checksumPath = Path.Combine(localPathRoot, Manifest.ChecksumFileName);
            UnityFS.DownloadTask.Create(Manifest.ChecksumFileName, null, 0, 0, checksumPath, 0, 10, checksumTask =>
            {
                var checksumJson = File.ReadAllText(checksumTask.path);
                try
                {
                    if (checksumJson.Length == 4)
                    {
                        // 兼容旧文件
                        GetManifestDirect(localPathRoot, checksumJson, 0, false, callback);
                    }
                    else
                    {
                        var fileEntry = JsonUtility.FromJson<FileEntry>(checksumJson);
                        GetManifestDirect(localPathRoot, fileEntry.checksum, fileEntry.size, true, callback);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("[GetManifest] failed to get checksum:{0}\n{1}", checksumJson, exception);
                }
            }).SetDebugMode(true).Run();
        }

        // 已知清单文件校验值和大小的情况下, 可以使用此接口, 略过 checksum 文件的获取 
        public static void GetManifestDirect(string localPathRoot, string checksum, int size, bool compressed, Action<Manifest> callback)
        {
            var manifestPath = Path.Combine(localPathRoot, Manifest.ManifestFileName);
            var manifest = ParseManifestFile(manifestPath, checksum, size, compressed);
            if (manifest != null)
            {
                callback(manifest);
            }
            else
            {
                UnityFS.DownloadTask.Create(Manifest.ManifestFileName, checksum, size, 0, manifestPath, 0, 10, manifestTask =>
                {
                    callback(ParseManifest(File.OpenRead(manifestTask.path), compressed));
                }).SetDebugMode(true).Run();
            }
        }

        // 打开指定的清单文件 (带校验)
        public static Manifest ParseManifestFile(string filePath, string checksum, int size, bool compressed)
        {
            if (File.Exists(filePath))
            {
                using (var fs = File.OpenRead(filePath))
                {
                    var crc = new Crc16();
                    crc.Update(fs);
                    if (crc.hex == checksum && fs.Length == size)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        return ParseManifest(fs, compressed);
                    }
                }
            }
            return null;
        }

        private static Manifest ParseManifestPlain(Stream stream)
        {
            var read = new MemoryStream();
            var bytes = new byte[256];
            do
            {
                var n = stream.Read(bytes, 0, bytes.Length);
                if (n <= 0)
                {
                    break;
                }
                read.Write(bytes, 0, n);
            } while (true);
            var data = read.ToArray();
            // Debug.LogFormat("read manifest data {0}", data.Length);
            var json = Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<Manifest>(json);
        }

        private static Manifest ParseManifest(Stream stream, bool compressed)
        {
            if (compressed)
            {
                using (var gz = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(stream))
                {
                    return ParseManifestPlain(gz);
                }
            }
            return ParseManifestPlain(stream);
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
            StreamingAssetsLoader streamingAssets,
            Action<int, int, ITask> onProgress,
            Action onComplete)
        {
            JobScheduler.DispatchCoroutine(DownloadBundlesCo(localPathRoot, bundles, streamingAssets, onProgress, onComplete));
        }

        // 当前任务数, 总任务数, 当前任务进度
        public static IEnumerator DownloadBundlesCo(
            string localPathRoot,
            Manifest.BundleInfo[] bundles,
            StreamingAssetsLoader streamingAssets,
            Action<int, int, ITask> onProgress,
            Action onComplete)
        {
            for (int i = 0, size = bundles.Length; i < size; i++)
            {
                var bundleInfo = bundles[i];
                if (streamingAssets != null && streamingAssets.Contains(bundleInfo.name, bundleInfo.checksum, bundleInfo.size))
                {
                    // Debug.LogWarning($"skipping embedded bundle {bundleInfo.name}");
                    continue;
                }
                var bundlePath = Path.Combine(localPathRoot, bundleInfo.name);
                var task = DownloadTask.Create(bundleInfo, bundlePath, -1, 10, null).SetDebugMode(true);
                var progress = -1.0f;
                task.Run();
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

        public static bool IsFileValid(string fullPath, string checksum, int size)
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
                        if (metadata != null && metadata.checksum == checksum && metadata.size == size)
                        {
                            return true;
                        }
                        File.Delete(metaPath);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogErrorFormat("[Exception] IsFileValie:{0} (checksum:{1} size:{2})\n{3}", fullPath, checksum, size, exception);
            }
            return false;
        }

        public static bool IsFileValid(string fullPath, FileEntry fileEntry)
        {
            return IsFileValid(fullPath, fileEntry.checksum, fileEntry.size);
        }

        // 检查本地 bundle 是否有效
        public static bool IsBundleFileValid(string fullPath, Manifest.BundleInfo bundleInfo)
        {
            return IsFileValid(fullPath, bundleInfo.checksum, bundleInfo.size);
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
