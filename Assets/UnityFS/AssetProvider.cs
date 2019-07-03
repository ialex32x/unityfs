using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProvider
    {
        UScene LoadScene(string assetPath);
        UScene LoadSceneAdditive(string assetPath);
        
        IFileSystem GetFileSystem(string bundleName);
        UAsset GetAsset(string assetPath);
        void Close();
    }
}
