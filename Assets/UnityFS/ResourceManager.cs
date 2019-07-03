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

        public static UBundle LoadBundle(string bundleName)
        {
            return _assetProvider.GetBundle(bundleName);
        }

        public static void ForEachTask(Action<ITask> callback)
        {
            _assetProvider.ForEachTask(callback);
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
    }
}
