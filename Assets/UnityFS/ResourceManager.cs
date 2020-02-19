using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public struct ResourceManagerArgs
    {
        public bool devMode;
        public int slow;
        public int bufferSize;
        public int concurrentTasks;
        public string localPathRoot;
        public Func<string, string> assetPathTransformer;
        public IList<string> urls;
        public Action oncomplete;
        public Action oninitialize;
    }

    public static class ResourceManager
    {
        // 资源加载器
        private static IAssetProvider _assetProvider;
        private static IAssetsAnalyzer _analyzer;
        private static IAssetProviderListener _listener;

        public static void SetListener(IAssetProviderListener listener)
        {
            _listener = listener;
        }

        public static IAssetProviderListener GetListener()
        {
            return _listener;
        }

        public static void SetAnalyzer(IAssetsAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public static IAssetsAnalyzer GetAnalyzer()
        {
            if (_analyzer == null)
            {
                _analyzer = new EmptyAssetsAnalyzer();
            }
            return _analyzer;
        }

        public static void Initialize(ResourceManagerArgs args)
        {
            _listener = new EmptyAssetProviderListener();
            UnityFS.JobScheduler.Initialize();
#if UNITY_EDITOR
            if (args.devMode)
            {
                _assetProvider = new UnityFS.AssetDatabaseAssetProvider();
            }
            else
#endif
            {
                _assetProvider = new UnityFS.BundleAssetProvider(args.localPathRoot, args.urls, args.concurrentTasks, args.slow, args.bufferSize, args.assetPathTransformer);
            }
            if (args.oninitialize != null)
            {
                args.oninitialize();
            }
            _assetProvider.Open();
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
    }
}
