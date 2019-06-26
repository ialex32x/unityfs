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

        public static void Initialize(IAssetProvider assetProvider)
        {
            _assetProvider = assetProvider;
            UnityFS.JobScheduler.Initialize();
        }

        public static IList<string> URLs(params string[] urls)
        {
            return new List<string>(urls);
        }

        public static UAsset LoadAsset(string assetPath)
        {
            return _assetProvider.GetAsset(assetPath);
        }

        public static Stream OpenFile(string filePath)
        {
            return _fileProvider.OpenFile(filePath);
        }
    }
}
