using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public static class ResourceManager
    {
        // 资源加载器
        static IAssetProvider _assetProvider;

        static IAssetsAnalyzer _analyzer;
        static IAssetProviderListener _listener;

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

        public static void Initialize(bool devMode, string localPathRoot, IList<string> urls, Action oncomplete)
        {
            Initialize(devMode, localPathRoot, urls, null, oncomplete);
        }

        public static void Initialize(bool devMode, string localPathRoot, IList<string> urls, Action oninitialize, Action oncomplete)
        {
            _listener = new EmptyAssetProviderListener();
            UnityFS.JobScheduler.Initialize();
#if UNITY_EDITOR
            if (devMode)
            {
                _assetProvider = new UnityFS.AssetDatabaseAssetProvider();
            }
            else
#endif
            {
                _assetProvider = new UnityFS.BundleAssetProvider(localPathRoot, urls);
            }
            if (oninitialize != null)
            {
                oninitialize();
            }
            _assetProvider.Open();
            if (oncomplete != null)
            {
                _assetProvider.completed += oncomplete;
            }
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
            return _assetProvider.LoadScene(assetPath);
        }

        public static UScene LoadSceneAdditive(string assetPath)
        {
            return _assetProvider.LoadSceneAdditive(assetPath);
        }

        public static UBundle LoadBundle(string bundleName)
        {
            return _assetProvider.GetBundle(bundleName);
        }

        public static void ForEachTask(Action<ITask> callback)
        {
            _assetProvider.ForEachTask(callback);
        }

        public static UAsset SearchAsset(string assetName)
        {
            throw new NotImplementedException();
        }

        public static UAsset LoadAsset(string assetPath)
        {
            return _assetProvider.GetAsset(assetPath);
        }

        public static UAsset LoadAsset(string assetPath, Action<UAsset> callback)
        {
            var asset = _assetProvider.GetAsset(assetPath);
            asset.completed += callback;
            return asset;
        }

        public static Utils.PrefabLoader Instantiate(string assetPath)
        {
            return Utils.PrefabLoader.Load(assetPath);
        }

        // 返回文件所在 FileSystem
        public static IFileSystem FindFileSystem(string assetPath)
        {
            var bundleName = _assetProvider.Find(assetPath);
            if (!string.IsNullOrEmpty(bundleName))
            {
                return GetFileSystem(bundleName);
            }
            return null;
        }

        public static IFileSystem GetFileSystem(string bundleName)
        {
            return _assetProvider.GetFileSystem(bundleName);
        }

        public static IFileSystem GetFileSystem(string bundleName, Action<IFileSystem> callback)
        {
            var fs = _assetProvider.GetFileSystem(bundleName);
            fs.completed += callback;
            return fs;
        }
    }
}
