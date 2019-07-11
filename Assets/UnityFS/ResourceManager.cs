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

        static IAssetProviderListener _listener;

        public static void SetListener(IAssetProviderListener listener)
        {
            _listener = listener;
        }

        public static IAssetProviderListener GetListener()
        {
            return _listener;
        }

        public static void Initialize(bool devMode, string localPathRoot, IList<string> urls, IAssetProviderListener listener)
        {
            _listener = listener ?? new EmptyAssetProviderListener();
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
        }

        public static void Close()
        {
            if (_assetProvider != null)
            {
                _assetProvider.Close();
            }
        }

        public static void Open()
        {
            _assetProvider.Open();
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
            return Utils.PrefabLoader.Instantiate(assetPath);
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
