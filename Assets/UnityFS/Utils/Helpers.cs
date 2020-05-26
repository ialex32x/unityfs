using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine.Networking;

namespace UnityFS.Utils
{
    using UnityEngine;

    public static class Helpers
    {
        private static LinkedList<UAsset> _roots = new LinkedList<UAsset>();

        public static void AddToRoot(UAsset asset)
        {
            _roots.AddLast(asset);
        }

        public static void RemoveFromRoot(UAsset asset)
        {
            _roots.Remove(asset);
        }

        public static void ValidateManifest(IList<string> urls, Action<int> callback, int retry = 0)
        {
            ResourceManager.ValidateManifest(urls, result => callback((int) result), retry);
        }

        public static string GetPlatformName()
        {
#if UNITY_EDITOR
            return GetBuildTargetName(UnityEditor.EditorUserBuildSettings.activeBuildTarget);
#else
            return GetPlatformName(Application.platform);
#endif
        }

        public static string GetPlatformName(RuntimePlatform runtimePlatform)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.Android: return "android";
                case RuntimePlatform.IPhonePlayer: return "ios";
                case RuntimePlatform.tvOS: return "tvos";
                case RuntimePlatform.WebGLPlayer: return "webgl";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer: return "windows";
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer: return "linux";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer: return "osx";
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerX86:
                case RuntimePlatform.WSAPlayerARM: return "wsa";
                case RuntimePlatform.PS4: return "ps4";
                case RuntimePlatform.XboxOne: return "xboxone";
                case RuntimePlatform.Switch: return "switch";
                default: return "unknown";
            }
        }

#if UNITY_EDITOR
        // 为目标平台命名
        public static string GetBuildTargetName(UnityEditor.BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case UnityEditor.BuildTarget.Android: return "android";
                case UnityEditor.BuildTarget.iOS: return "ios";
                case UnityEditor.BuildTarget.tvOS: return "tvos";
                case UnityEditor.BuildTarget.WebGL: return "webgl";
                case UnityEditor.BuildTarget.StandaloneWindows:
                case UnityEditor.BuildTarget.StandaloneWindows64: return "windows";
                case UnityEditor.BuildTarget.StandaloneLinux:
                case UnityEditor.BuildTarget.StandaloneLinux64:
                case UnityEditor.BuildTarget.StandaloneLinuxUniversal: return "linux";
                case UnityEditor.BuildTarget.StandaloneOSX: return "osx";
                case UnityEditor.BuildTarget.WSAPlayer: return "wsa";
                case UnityEditor.BuildTarget.PS4: return "ps4";
                case UnityEditor.BuildTarget.XboxOne: return "xboxone";
                case UnityEditor.BuildTarget.Switch: return "switch";
                default: return "unknown";
            }
        }
#endif

        // 比对两个 FileEntry 记录是否相同
        public static bool IsFileEntryEquals(FileEntry fileEntry1, FileEntry fileEntry2)
        {
            return fileEntry1 != null
                   && fileEntry2 != null
                   && fileEntry1.checksum == fileEntry2.checksum
                   && fileEntry1.size == fileEntry2.size
                   && fileEntry1.rsize == fileEntry2.rsize;
        }

        // 比对两个 ManifestEntry 记录是否相同
        public static bool IsManifestEntryEquals(ManifestEntry fileEntry1, ManifestEntry fileEntry2)
        {
            return fileEntry1 != null
                   && fileEntry2 != null
                   && fileEntry1.checksum == fileEntry2.checksum
                   && fileEntry1.size == fileEntry2.size
                   && fileEntry1.rsize == fileEntry2.rsize
                   && fileEntry1.chunkSize == fileEntry2.chunkSize;
        }

        // 基本流程:
        // 在不知道清单文件校验值和大小的情况下, 使用此接口尝试先下载 checksum 文件, 得到清单文件信息
        public static void GetManifest(string localPathRoot, DownloadWorker worker, string checksum, int size,
            int rsize, string password, int chunkSize, 
            Action<Manifest, ManifestEntry> callback)
        {
            if (checksum != null && size > 0 && rsize > 0 && chunkSize > 0)
            {
                var fileEntry = new ManifestEntry()
                {
                    name = Manifest.ManifestFileName,
                    checksum = checksum,
                    size = size,
                    rsize = rsize, 
                    chunkSize = chunkSize, 
                };
                GetManifestDirect(localPathRoot, worker, fileEntry, password, callback);
                return;
            }

            if (!Directory.Exists(localPathRoot))
            {
                Directory.CreateDirectory(localPathRoot);
            }

            ReadRemoteFile(ResourceManager.urls, Manifest.ChecksumFileName, content =>
            {
                try
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        Debug.LogWarning("checksum 无内容");
                    }
                    else
                    {
                        var fileEntry = JsonUtility.FromJson<ManifestEntry>(content);
                        if (fileEntry != null)
                        {
                            // Debug.LogFormat("checksum {0} {1}", fileEntry.checksum, fileEntry.size);
                            GetManifestDirect(localPathRoot, worker, fileEntry, password, callback);
                            return true;
                        }

                        Debug.LogWarningFormat("checksum 无法解析 {0}", content);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("checksum 解析异常 {0}\n{1}", content, exception);
                }

                return false;
            });
        }

        // 已知清单文件校验值和大小的情况下, 可以使用此接口, 略过 checksum 文件的获取 
        public static void GetManifestDirect(string localPathRoot, DownloadWorker worker, ManifestEntry fileEntry,
            string password,
            Action<Manifest, ManifestEntry> callback)
        {
            var manifestPath = Path.Combine(localPathRoot, Manifest.ManifestFileName);
            var manifest_t = ParseManifestFile(manifestPath, fileEntry, password);
            if (manifest_t != null)
            {
                callback(manifest_t, fileEntry);
            }
            else
            {
                var manifestJob = new DownloadWorker.JobInfo(Manifest.ManifestFileName, fileEntry.checksum, "Manifest",
                    0, fileEntry.size, manifestPath)
                {
                    emergency = true,
                    callback = () =>
                    {
                        var manifest = ParseManifestStream(File.OpenRead(manifestPath), fileEntry, password);
                        callback(manifest, fileEntry);
                    }
                };
                worker.AddJob(manifestJob);
            }
        }

        // 打开指定的清单文件 (带校验)
        public static Manifest ParseManifestFile(string filePath, ManifestEntry fileEntry, string password)
        {
            if (File.Exists(filePath))
            {
                using (var fs = File.OpenRead(filePath))
                {
                    var crc = new Crc16();
                    crc.Update(fs);
                    if (crc.hex == fileEntry.checksum && fs.Length == fileEntry.size)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        return ParseManifestStream(fs, fileEntry, password);
                    }
                }
            }

            return null;
        }

        private static Manifest ParseManifestStream(Stream secStream, ManifestEntry fileEntry, string password)
        {
            secStream.Seek(0, SeekOrigin.Begin);
            using (var zStream = GetDecryptStream(secStream, fileEntry, password))
            {
                using (var gz = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(zStream))
                {
                    var read = new MemoryStream();
                    var bytes = new byte[256];
                    do
                    {
                        var n = gz.Read(bytes, 0, bytes.Length);
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
            }
        }

        public static List<Manifest.BundleInfo> CollectStartupBundles(Manifest manifest, string localPathRoot)
        {
            return CollectBundles(manifest, localPathRoot, info => info.startup);
        }

        public static List<Manifest.BundleInfo> CollectBundles(Manifest manifest, string localPathRoot,
            Func<Manifest.BundleInfo, bool> filter)
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

            return pending;
        }

        // 检查本地文件是否有效 (此接口仅通过本地meta文件验证对应文件是否有效)
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
                        if (metadata != null && metadata.checksum == checksum && (size == 0 || metadata.size == size))
                        {
                            return true;
                        }

                        File.Delete(metaPath);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogErrorFormat("[Exception] IsFileValie:{0} (checksum:{1} size:{2})\n{3}", fullPath, checksum,
                    size, exception);
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
                    var fileStream = File.OpenRead(fullPath);
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
            var list = new List<string>();
            var platform = GetPlatformName();
            for (int i = 0, size = urls.Length; i < size; i++)
            {
                var item = urls[i];
                if (item.EndsWith("/"))
                {
                    list.Add(item + platform);
                }
                else
                {
                    list.Add(item + "/" + platform);
                }
            }

            return list;
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

        public static Stream GetDecryptStream(Stream fin, ManifestEntry fileEntry, string password)
        {
            return GetDecryptStream(fin, fileEntry.name, fileEntry.size, fileEntry.rsize, fileEntry.chunkSize,
                password);
        }

        public static Stream GetDecryptStream(Stream fin, Manifest.BundleInfo bundleInfo, string password, int chunkSize)
        {
            if (bundleInfo.encrypted)
            {
                return GetDecryptStream(fin, bundleInfo.name, bundleInfo.size, bundleInfo.rsize, chunkSize,
                    password);
            }

            return fin;
        }

        public static Stream GetDecryptStream(Stream fin, string name, int size, int rsize, int chunkSize,
            string password)
        {
            var phrase = password + name;
            var key = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(phrase));
            var iv = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(phrase + Manifest.EncryptionSalt));
            var chunkStream = new ChunkedStream(key, iv, fin, rsize, chunkSize);
            return chunkStream;
        }

        public static string GetStreamingAssetsPath(string innerPath)
        {
            var streamingAssetsPathRoot = Application.streamingAssetsPath + EnsureSperator(innerPath);
            if (Application.platform != RuntimePlatform.Android)
            {
                streamingAssetsPathRoot = "file://" + streamingAssetsPathRoot;
            }

            return streamingAssetsPathRoot;
        }

        public static string GetStreamingAssetsFilePath(string innerPath)
        {
            var streamingAssetsPathRoot = Application.streamingAssetsPath + EnsureFileSperator(innerPath);
            if (Application.platform != RuntimePlatform.Android)
            {
                streamingAssetsPathRoot = "file://" + streamingAssetsPathRoot;
            }

            return streamingAssetsPathRoot;
        }

        public static string EnsureFileSperator(string name)
        {
            var len = name.Length;
            if (len > 0)
            {
                if (name[0] != '/')
                {
                    return '/' + name;
                }
            }

            return name;
        }

        public static string EnsureSperator(string name)
        {
            var len = name.Length;
            if (len > 0)
            {
                if (name[0] != '/')
                {
                    if (name[len - 1] != '/')
                    {
                        return '/' + name + '/';
                    }

                    return '/' + name;
                }

                if (name[len - 1] != '/')
                {
                    return name + '/';
                }
            }

            return name;
        }

        // 指定的文件清单复制到本地目录
        public static IEnumerator CopyStreamingAssets(string outputPath, FileListManifest fileListManifest,
            Action oncomplete)
        {
            var count = fileListManifest.files.Count;
            for (var i = 0; i < count; i++)
            {
                var file = fileListManifest.files[i];
                var outputFile = Path.Combine(outputPath, file.name);
                if (!File.Exists(outputFile))
                {
                    var streamingAssetsFilePath = GetStreamingAssetsFilePath(file.name);
                    var uwr = UnityWebRequest.Get(streamingAssetsFilePath);
                    yield return uwr.SendWebRequest();
                    if (uwr.error == null && uwr.responseCode == 200)
                    {
                        try
                        {
                            var bytes = uwr.downloadHandler.data;
                            var checksum = Crc16.ComputeChecksum(bytes);
                            var size = bytes.Length;
                            var metaFile = Path.Combine(outputPath, file.name + Metadata.Ext);
                            var metadata = new Metadata()
                            {
                                checksum = Crc16.ToString(checksum),
                                size = size,
                            };
                            var metaJson = JsonUtility.ToJson(metadata);
                            File.WriteAllText(metaFile, metaJson);
                            File.WriteAllBytes(outputFile, bytes);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarningFormat("CopyStreamingAssets exception: {0}\n{1}",
                                streamingAssetsFilePath, exception);
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("CopyStreamingAssets request failed {0}: {1} {2}",
                            streamingAssetsFilePath, uwr.responseCode,
                            uwr.error);
                    }
                }
            }

            oncomplete?.Invoke();
        }

        // callback 返回 true 表示停止重试或完成
        public static void ReadRemoteFile(IList<string> urls, string filename, Func<string, bool> callback)
        {
            JobScheduler.DispatchCoroutine(_ReadRemoteFile(urls, filename, callback));
        }

        private static IEnumerator _ReadRemoteFile(IList<string> urls, string filename, Func<string, bool> callback)
        {
            var urlIndex = 0;
            var url = "";

            while (true)
            {
                if (urlIndex < urls.Count)
                {
                    if (urls[urlIndex].EndsWith("/"))
                    {
                        url = urls[urlIndex] + filename;
                    }
                    else
                    {
                        url = urls[urlIndex] + "/" + filename;
                    }

                    ++urlIndex;
                    url += "?checksum=" + DateTime.Now.Ticks;
                }

                var uwr = UnityWebRequest.Get(url);
                yield return uwr.SendWebRequest();
                try
                {
                    if (uwr.error == null)
                    {
                        var handler = uwr.downloadHandler;
                        if (callback(handler.text))
                        {
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("retry {0}: {1}", url, uwr.error);
                        if (callback(null))
                        {
                            yield break;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("exception {0}: {1}", url, exception);
                }

                yield return new WaitForSeconds(1.5f);
            }
        }

        // Example: 空闲时执行下载
        public static IEnumerator _IdleDownload()
        {
            var bundles = ResourceManager.GetInvalidatedBundles();
            var size = bundles.Count;
            var wait = new WaitForSeconds(30f);
            yield return new WaitForSeconds(15f);
            for (var i = 0; i < size; i++)
            {
                var bundle = bundles[i];
                var job = ResourceManager.EnsureBundle(bundle);
                if (job != null)
                {
                    Debug.LogFormat("idle download: {0}", bundle.name);
                    while (!job.isDone)
                    {
                        yield return null;
                    }
                }

                yield return wait;
            }
        }
    }
}