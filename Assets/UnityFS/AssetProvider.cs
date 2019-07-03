using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProvider
    {
        void ForEachTask(Action<ITask> callback);

        UScene LoadScene(string assetPath);
        UScene LoadSceneAdditive(string assetPath);

        IFileSystem GetFileSystem(string bundleName);
        UBundle GetBundle(string bundleName);
        UAsset GetAsset(string assetPath);
        void Close();
    }
}
