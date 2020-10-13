using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public enum EValidationResult
    {
        Latest, // 已经最新
        Update, // 有更新
        Failed, // 失败
    }

    // 清单
    [Serializable]
    public class Manifest
    {
        public const string ManifestFileName = "manifest.pkg";
        public const string ChecksumFileName = "checksum.txt";
        public const string EmbeddedManifestFileName = "streamingassets-manifest.json";
        public const string EmbeddedBundlesBasePath = "packages";
        public const string EncryptionSalt = "SALT";
        public const string AssetListDataExt = ".assetlistdata";

        [Serializable]
        [Flags]
        public enum BundleLoad
        {
            Startup = 1 << 0,
            Important = 1 << 1,
            Normal = 1 << 2,
            Optional = 1 << 3,
            
            Any = Startup | Important | Normal | Optional,
        }

        [Serializable]
        public enum BundleType
        {
            AssetBundle = 0, // 打资源 ab 包
            ZipArchive = 1,  // 打 zip 包

            // SceneBundle,     // 打场景 ab 包
            FileList = 2, // 仅生成文件清单
            RawFile = 3,  // 原文件 (功能未实现) 
        }

        // 资源包清单
        [Serializable]
        public class BundleInfo
        {
            public BundleType type; // 资源包类型
            public BundleLoad load; // 加载级别

            public bool startup => load == BundleLoad.Startup; // 是否需要在启动前完成下载更新

            public bool encrypted;
            public int rsize; // 加密文件的原始大小
            public int priority; // 下载排队优先级
            public string name; // 文件名
            public int size; // 文件大小
            public string checksum; // 文件校验值
            public string comment;
            public string tag;
            public string[] dependencies; // 依赖的 bundle
            public List<string> assets = new List<string>(); // asset path (virtual path)
        }

        public int chunkSize;
        public int build; // 资源版本 (资源打包次数)
        public int timestamp; // epoch time
        public string tag;
        public List<BundleInfo> bundles = new List<BundleInfo>(); // bundle 清单
    }

    [Serializable]
    public class FileEntry
    {
        public string name;
        public int size;
        public int rsize;
        public string checksum;
    }

    [Serializable]
    public class ManifestEntry
    {
        public string name;
        public int size;
        public int rsize;
        public string checksum;
        public int chunkSize;
    }

    // for streamingassets reader
    [Serializable]
    public class EmbeddedManifest
    {
        public List<FileEntry> bundles = new List<FileEntry>(); // bundle 清单
    }

    [Serializable]
    public class FileListManifest
    {
        public List<FileEntry> files = new List<FileEntry>();
    }
}