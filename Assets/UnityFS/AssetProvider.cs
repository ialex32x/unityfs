using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProvider
    {
        IFileSystem GetFileSystem(string bundleName);
        UAsset GetAsset(string assetPath);
        void Close();
    }
}
