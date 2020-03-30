using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class ResourceManagerArgs
    {
        public bool devMode;
        public int bytesPerSecond = 1024 * 768; // 常规下载速度
        public int bytesPerSecondIdle = 256 * 1024; // 空闲任务下载速度 
        public int bufferSize;
        public string localPathRoot;
        public string manifestChecksum;
        public int manifestSize;
        public int manifestRSize;
        public Func<string, string> assetPathTransformer;
        public IList<string> urls;
        public Action oncomplete;
        public Action oninitialize;

        public float asyncSimMin; // 伪装异步加载
        public float asyncSimMax;
        public string listDataPath;
        public string password;
    }

    public static class ResourceManager
    {
        private static List<string> _urls = new List<string>();

        // 资源加载器
        private static IAssetProvider _assetProvider;
        private static Analyzer.IAssetsAnalyzer _analyzer;
        private static IAssetProviderListener _listener;

        public static string tag
        {
            get { return _assetProvider?.tag; }
        }

        public static IList<string> urls
        {
            get { return _urls; }
            set
            {
                _urls.Clear();
                _urls.AddRange(value);
            }
        }

        public static void SetListener(IAssetProviderListener listener)
        {
            _listener = listener;
        }

        public static IAssetProviderListener GetListener()
        {
            return _listener;
        }

        public static Analyzer.IAssetsAnalyzer GetAnalyzer()
        {
            return _analyzer;
        }

        private static string NormalizedListPath(string fileName)
        {
            if (!fileName.EndsWith(".asset"))
            {
                fileName += ".asset";
            }

            if (!fileName.StartsWith("Assets/"))
            {
                fileName = "Assets/" + fileName;
            }

            return fileName;
        }

        public static void Initialize(ResourceManagerArgs args)
        {
            _urls.AddRange(args.urls);
            _listener = new EmptyAssetProviderListener();
            JobScheduler.Initialize();
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(args.listDataPath))
            {
                var listData =
                    UnityEditor.AssetDatabase.LoadMainAssetAtPath(NormalizedListPath(args.listDataPath)) as
                        AssetListData;
                if (listData != null)
                {
                    _analyzer = new Analyzer.DefaultAssetsAnalyzer(listData);
                }
            }

            if (args.devMode)
            {
                _assetProvider = new UnityFS.AssetDatabaseAssetProvider(args.asyncSimMin, args.asyncSimMax);
            }
            else
#endif
            {
                _assetProvider = new UnityFS.BundleAssetProvider(args);
            }

            args.oninitialize?.Invoke();
            _assetProvider.Open(args);
            if (args.oncomplete != null)
            {
                _assetProvider.completed += args.oncomplete;
            }
        }

        public static IAssetProvider GetAssetProvider()
        {
#if UNITY_EDITOR
            if (_assetProvider == null)
            {
                Debug.LogWarning("[EditorOnly] ResourceManager 未初始化时使用了资源接口, 默认采用编辑器模式运行.");
                Initialize(new ResourceManagerArgs()
                {
                    devMode = true,
                });
            }
#endif
            return _assetProvider;
        }

        public static void Close()
        {
            if (_assetProvider != null)
            {
                _assetProvider.Close();
            }
        }

        public static UScene LoadScene(string assetPath)
        {
            return GetAssetProvider().LoadScene(assetPath);
        }

        public static UScene LoadSceneAdditive(string assetPath)
        {
            return GetAssetProvider().LoadSceneAdditive(assetPath);
        }

        public static UBundle LoadBundle(string bundleName)
        {
            return GetAssetProvider().GetBundle(bundleName);
        }

        public static void ForEachTask(Action<ITask> callback)
        {
            GetAssetProvider().ForEachTask(callback);
        }

        public static UAsset SearchAsset(string assetName)
        {
            throw new NotImplementedException();
        }

        // 资源包是否立即可用 (本地有效)
        public static bool IsBundleAvailable(string bundleName)
        {
            return GetAssetProvider().IsBundleAvailable(bundleName);
        }

        public static void ValidateManifest(Action<EValidationResult> callback, int retry = 0)
        {
            var provider = GetAssetProvider() as BundleAssetProvider;
            if (provider != null)
            {
                provider.ValidateManifest(retry, callback);
            }
            else
            {
                callback(EValidationResult.Latest);
            }
        }

        // 资源是否立即可用 (本地有效)
        public static bool IsAssetAvailable(string assetPath)
        {
            return GetAssetProvider().IsAssetAvailable(assetPath);
        }

        public static bool IsAssetExists(string assetPath)
        {
            return GetAssetProvider().IsAssetExists(assetPath);
        }

        public static UAsset LoadAsset(string assetPath)
        {
            return GetAssetProvider().GetAsset(assetPath, null);
        }

        public static UAsset LoadAsset(string assetPath, Type type)
        {
            return GetAssetProvider().GetAsset(assetPath, type);
        }

        public static UAsset LoadAsset(string assetPath, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, null);
            asset.completed += callback;
            return asset;
        }

        public static UAsset LoadAsset(string assetPath, Type type, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, type);
            asset.completed += callback;
            return asset;
        }

        /// 一次性加载若干个资源
        public static UAssets LoadAssets(IList<string> assetPaths)
        {
            return new UAssets().AddRange(assetPaths);
        }

        public static Utils.PrefabLoader Instantiate(string assetPath)
        {
            return Utils.PrefabLoader.Load(assetPath);
        }

        // 返回文件所在 FileSystem
        public static IFileSystem FindFileSystem(string assetPath)
        {
            var bundleName = GetAssetProvider().Find(assetPath);
            if (!string.IsNullOrEmpty(bundleName))
            {
                return GetFileSystem(bundleName);
            }

            return null;
        }

        public static IFileSystem GetFileSystem(string bundleName)
        {
            return GetAssetProvider().GetFileSystem(bundleName);
        }

        public static IFileSystem GetFileSystem(string bundleName, Action<IFileSystem> callback)
        {
            var fs = GetAssetProvider().GetFileSystem(bundleName);
            fs.completed += callback;
            return fs;
        }

        public static IList<DownloadWorker.JobInfo> EnsureBundles(Manifest.BundleLoad load, Action onComplete)
        {
            return GetAssetProvider().EnsureBundles(load, onComplete);
        }

        // 下载指定的资源包 (返回 null 表示不需要下载)
        public static DownloadWorker.JobInfo EnsureBundle(Manifest.BundleInfo bundleInfo)
        {
            return GetAssetProvider().EnsureBundle(bundleInfo);
        }

        // 检查本地资源包状态, 返回所有需要下载的包信息的列表
        public static IList<Manifest.BundleInfo> GetInvalidatedBundles()
        {
            return GetAssetProvider().GetInvalidatedBundles();
        }
    }
}