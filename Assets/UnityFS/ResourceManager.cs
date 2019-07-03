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

        public static void Initialize()
        {
            UnityFS.JobScheduler.Initialize();
        }

        public static void Open(IAssetProvider assetProvider)
        {
            _assetProvider = assetProvider;
        }

        public static UScene LoadScene(string assetPath)
        {
            return _assetProvider.LoadScene(assetPath);
        }

        public static UScene LoadSceneAdditive(string assetPath)
        {
            return _assetProvider.LoadSceneAdditive(assetPath);
        }

        public static UAsset LoadAsset(string assetPath)
        {
            return _assetProvider.GetAsset(assetPath);
        }

        public static void LoadAsset(string assetPath, Action<UAsset> callback)
        {
            var asset = _assetProvider.GetAsset(assetPath);
            asset.completed += callback;
        }

        public static Utils.PrefabLoader Instantiate(string assetPath)
        {
            return Utils.PrefabLoader.Instantiate(assetPath);
        }

        public static UBundle GetBundle(string bundleName)
        {
            var provider = _assetProvider as BundleAssetProvider;
            if (provider != null)
            {
                return provider.GetBundle(bundleName);
            }
            throw new InvalidOperationException();
        }

        public static IFileSystem GetFileSystem(string bundleName)
        {
            return _assetProvider.GetFileSystem(bundleName);
        }
    }
}
