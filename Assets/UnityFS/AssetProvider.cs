using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProvider
    {
        // 查找资源, 返回其所在的包
        string Find(string assetPath);
        void ForEachTask(Action<ITask> callback);

        UScene LoadScene(string assetPath);
        UScene LoadSceneAdditive(string assetPath);

        IFileSystem GetFileSystem(string bundleName);
        UBundle GetBundle(string bundleName);
        UAsset GetAsset(string assetPath);
        void Close();
    }
}
