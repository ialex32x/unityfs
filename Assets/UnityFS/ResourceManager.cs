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
        public int manifestChunkSize;   
        public Func<string, string> assetPathTransformer;
        public IList<string> urls;
        public Action oncomplete;
        public Action oninitialize;

        public float asyncSimMin; // 伪装异步加载
        public float asyncSimMax;
        public string listDataPath;
        public string password;
        public bool useBaseManifest; // base manifest in StreamingAssets
    }

    public static class ResourceManager
    {
        private static List<string> _urls = new List<string>();

        // 资源加载器
        private static IAssetProvider _assetProvider;
        private static ILogger _logger;
        private static Analyzer.IAssetsAnalyzer _analyzer;
        private static IAssetProviderListener _listener;
        private static List<DownloadWorker> _allWorkers = new List<DownloadWorker>();

        public static string tag
        {
            get { return _assetProvider?.tag; }
        }

        public static int build
        {
            get { return _assetProvider != null ? _assetProvider.build : -1; }
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

        public static ILogger logger
        {
            get
            {
                if (_logger == null)
                {
                    _logger=new DefaultLogger();
                }
                
                return _logger;
            }
        }

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
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
            if (!fileName.EndsWith(Manifest.AssetListDataExt))
            {
                fileName += Manifest.AssetListDataExt;
            }

            if (!fileName.StartsWith("Assets/"))
            {
                fileName = "Assets/" + fileName;
            }

            return fileName;
        }

        public static void Initialize(ResourceManagerArgs args)
        {
            _urls.Clear();
            _urls.AddRange(args.urls);
            if (_listener == null)
            {
                _listener = new EmptyAssetProviderListener();
            }

            JobScheduler.Initialize();
            
            if (_assetProvider != null)
            {
                _assetProvider.Close();
            }

#if UNITY_EDITOR
            if (_analyzer == null)
            {
                if (!string.IsNullOrEmpty(args.listDataPath))
                {
                    var listDataPath = NormalizedListPath(args.listDataPath);
                    _analyzer = new Analyzer.DefaultAssetsAnalyzer(listDataPath);
                }
            }

            if (_analyzer != null)
            {
                _analyzer.Begin();
            }

            if (args.devMode)
            {
                _assetProvider = new AssetDatabaseAssetProvider(args.asyncSimMin, args.asyncSimMax);
            }
            else
#endif
            {
                _assetProvider = new BundleAssetProvider();
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
            var size = _allWorkers.Count;
            if (size > 0)
            {
                var workers = new DownloadWorker[size];
                _allWorkers.CopyTo(workers, 0);
                for (var i = 0; i < size; i++)
                {
                    workers[i].Abort();
                }

                _allWorkers.Clear();
            }

            if (_assetProvider != null)
            {
                _assetProvider.Close();
            }

            if (_analyzer != null)
            {
                _analyzer.End();
            }

            JobScheduler.Clear();
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

        public static void ValidateManifest(IList<string> urls, Action<EValidationResult> callback, int retry = 0)
        {
            var provider = GetAssetProvider() as BundleAssetProvider;
            if (provider != null)
            {
                provider.ValidateManifest(urls, retry, callback);
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
            return GetAssetProvider().GetAsset(assetPath, null, EAssetHints.None);
        }

        public static UAsset LoadAsset(string assetPath, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, null, EAssetHints.None);
            asset.completed += callback;
            return asset;
        }

        public static UAsset TryLoadAssetSync(string assetPath)
        {
            return GetAssetProvider().GetAsset(assetPath, null, EAssetHints.Synchronized);
        }

        public static UAsset TryLoadAssetSync(string assetPath, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, null, EAssetHints.Synchronized);
            asset.completed += callback;
            return asset;
        }

        public static UAsset LoadAsset<T>(string assetPath)
        {
            return GetAssetProvider().GetAsset(assetPath, typeof(T), EAssetHints.None);
        }

        public static UAsset LoadAsset<T>(string assetPath, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, typeof(T), EAssetHints.None);
            asset.completed += callback;
            return asset;
        }

        public static UAsset TryLoadAssetSync<T>(string assetPath)
        {
            return GetAssetProvider().GetAsset(assetPath, typeof(T), EAssetHints.Synchronized);
        }

        public static UAsset TryLoadAssetSync<T>(string assetPath, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, typeof(T), EAssetHints.Synchronized);
            asset.completed += callback;
            return asset;
        }

        public static UAsset LoadAsset(string assetPath, Type type)
        {
            return GetAssetProvider().GetAsset(assetPath, type, EAssetHints.None);
        }

        public static UAsset LoadAsset(string assetPath, Type type, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, type, EAssetHints.None);
            asset.completed += callback;
            return asset;
        }

        public static UAsset TryLoadAssetSync(string assetPath, Type type)
        {
            return GetAssetProvider().GetAsset(assetPath, type, EAssetHints.Synchronized);
        }

        public static UAsset TryLoadAssetSync(string assetPath, Type type, Action<UAsset> callback)
        {
            var asset = GetAssetProvider().GetAsset(assetPath, type, EAssetHints.Synchronized);
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

        public static IList<DownloadWorker.JobInfo> EnsureBundles(IList<Manifest.BundleInfo> bundleInfos, Action onComplete)
        {
            return GetAssetProvider().EnsureBundles(bundleInfos, onComplete);
        }

        [Obsolete("use IList<DownloadWorker.JobInfo> EnsureBundles(IList<Manifest.BundleInfo> bundleInfos, Action onComplete) instead.")]
        public static IList<DownloadWorker.JobInfo> EnsureBundles(Manifest.BundleLoad load, Action onComplete)
        {
            var bundleInfos = GetInvalidatedBundles(load);
            return GetAssetProvider().EnsureBundles(bundleInfos, onComplete);
        }

        // 下载指定的资源包 (返回 null 表示不需要下载)
        public static DownloadWorker.JobInfo EnsureBundle(Manifest.BundleInfo bundleInfo)
        {
            return GetAssetProvider().EnsureBundle(bundleInfo);
        }

        [Obsolete("use IList<Manifest.BundleInfo> GetInvalidatedBundles(Manifest.BundleLoad load) instead.")]
        public static IList<Manifest.BundleInfo> GetInvalidatedBundles()
        {
            return GetAssetProvider().GetInvalidatedBundles(Manifest.BundleLoad.Any);
        }

        public static void CollectAssets(List<UAsset> assets)
        {
            GetAssetProvider().CollectAssets(assets);
        }
        
        // 检查本地资源包状态, 返回所有需要下载的包信息的列表
        public static IList<Manifest.BundleInfo> GetInvalidatedBundles(Manifest.BundleLoad load)
        {
            return GetAssetProvider().GetInvalidatedBundles(load);
        }

        public static void AddWorker(DownloadWorker worker)
        {
            _allWorkers.Add(worker);
        }

        public static void RemoveWorker(DownloadWorker worker)
        {
            _allWorkers.Remove(worker);
        }
    }
}