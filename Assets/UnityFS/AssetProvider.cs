using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProvider
    {
        event Action completed;

        void EnsureBundles(Manifest.BundleLoad load, Action onComplete);

        // 查找资源, 返回其所在的包
        string Find(string assetPath);
        void ForEachTask(Action<ITask> callback);

        UScene LoadScene(string assetPath);
        UScene LoadSceneAdditive(string assetPath);

        IFileSystem GetFileSystem(string bundleName);
        UBundle GetBundle(string bundleName);
        UAsset GetAsset(string assetPath, Type type);

        // 资源是否立即可用 (本地有效)
        bool IsAssetAvailable(string assetPath);
        
        bool IsAssetExists(string assetPath);

        // 资源包是否立即可用 (本地有效)
        bool IsBundleAvailable(string bundleName);

        void Open(ResourceManagerArgs args);
        void Close();
    }
}
