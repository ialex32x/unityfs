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

        // 文件加载器
        static IFileProvider _fileProvider;

        // 下载器
        static IDownloader _downloader;

        public static void Initialize()
        {
        }

        public static void SetAssetProvider(IAssetProvider assetProvider)
        {
            _assetProvider = assetProvider;
        }

        public static void SetFileProvider(IFileProvider fileProvider)
        {
            _fileProvider = fileProvider;
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
