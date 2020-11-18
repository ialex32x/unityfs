using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public partial class BundleBuilderData : ScriptableObject
    {
        public const string StreamingAssetsPath = "Assets/StreamingAssets";
        public const string BundleBuilderDataPath = "Assets/unityfs.asset";
        public const string FileExt = ".pkg";

        [Serializable]
        public class BundleAssetTarget
        {
            public int id;
            public bool enabled = true;

            public string targetPath;

            public PackagePlatform platform; // 在特定平台中生效
            // public BundleAssetTypes types = (BundleAssetTypes)~0; // (仅搜索目录时) 仅包含指定资源类型
            // public List<string> extensions = new List<string>();  // (仅搜索目录时) 额外包含指定后缀的文件

            public bool IsBuildPlatform(PackagePlatform buildPlatform)
            {
                return platform == PackagePlatform.Any || platform == buildPlatform;
            }
        }

        [Serializable]
        public enum BundleSplitType
        {
            None,
            Prefix, // 资源名前缀
            Suffix, // 资源名后缀 (不含扩展名)
            FileSuffix, // 文件完整名后缀
            PathPrefix, // 路径前缀 
        }

        [Serializable]
        public class BundleSplitRule
        {
            public BundleSplitType type;
            public BundleAssetTypes assetTypes;
            public string keyword;
            public bool exclude;
        }

        public int id;
        public int build; // 版本 (打包次数)
        public string encryptionKey;
        public int chunkSize = 4096;
        public List<BundleInfo> bundles = new List<BundleInfo>();
        [SerializeField]
        private AssetAttributesMap assetAttributesMap = new AssetAttributesMap();
        public string assetBundlePath = "out/bundles";
        public string zipArchivePath = "out/zipArchives";
        public string packagePath = "out/packages";
        public int priorityMax = 10000;
        // public int searchMax = 200;
        public bool showBundleDetails = false;
        public bool disableTypeTree = false;
        public bool lz4Compression = true;
        public bool deterministicAssetBundle = true;
        public bool extractShaderVariantCollections = true; // 自动展开 shaderVariantCollections 中包含的 shader
        public bool streamingAssetsAnyway = false; // 无视 bundleInfo/bundleSlice 的设置, 默认认为进入 StreamingAssets
        public bool streamingAssetsManifest = false; // 是否将清单本身复制到 StreamingAssets

        [SerializeField]
        private string _mainAssetListPath;

        private bool _assetListDataLoaded;
        private AssetListData _assetListData;

        public string mainAssetListPath
        {
            get { return _mainAssetListPath; }
            set
            {
                if (_mainAssetListPath != value)
                {
                    _mainAssetListPath = value;
                    _assetListDataLoaded = false;
                    MarkAsDirty();
                }
            }
        }

        public AssetListData assetListData
        {
            get
            {
                if (!_assetListDataLoaded)
                {
                    _assetListData = AssetListData.ReadFrom(_mainAssetListPath);
                    _assetListDataLoaded = true;
                }
                return _assetListData;
            }
        }

        public SList skipExts = new SList(".xlsx", ".xlsm", ".xls", ".docx", ".doc", ".cs");

        public static BundleBuilderData Load()
        {
            var data = AssetDatabase.LoadMainAssetAtPath(BundleBuilderDataPath) as BundleBuilderData;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BundleBuilderData>();
                AssetDatabase.CreateAsset(data, BundleBuilderDataPath);
                AssetDatabase.SaveAssets();
            }

            return data;
        }

        public void ForEachAssetPath(Action<BundleInfo, BundleSplit, BundleSlice, string> visitor)
        {
            for (int i = 0, size = bundles.Count; i < size; i++)
            {
                var bundle = bundles[i];
                bundle.ForEachAssetPath((split, slice, assetPath) => visitor(bundle, split, slice, assetPath));
            }
        }

        public AssetAttributes AddAssetPathAttributes(string assetPath)
        {
            AssetAttributes attrs;
            if (!assetAttributesMap.TryGetValue(assetPath, out attrs))
            {
                attrs = assetAttributesMap[assetPath] = new AssetAttributes();
                MarkAsDirty();
            }
            return attrs;
        }

        public bool RemoveAssetPathAttributes(string assetPath)
        {
            if (assetAttributesMap.Remove(assetPath))
            {
                MarkAsDirty();
                return true;
            }

            return false;
        }

        public AssetAttributes GetAssetPathAttributes(string assetPath)
        {
            AssetAttributes attrs;
            return assetAttributesMap.TryGetValue(assetPath, out attrs) ? attrs : null;
        }

        public void MarkAsDirty()
        {
            EditorUtility.SetDirty(this);
        }

        public void Cleanup()
        {
            // allCollectedAssets.Clear();
            // ArrayUtility.Clear(ref allCollectedAssetsPath);
            foreach (var bundle in bundles)
            {
                bundle.Cleanup();
                if (bundle.splits.Count == 0)
                {
                    var defaultSplit = new BundleSplit();
                    bundle.splits.Add(defaultSplit);
                    MarkAsDirty();
                }
            }
        }

        public bool IsPackAsset(string assetPath)
        {
            var assetAttributes = GetAssetPathAttributes(assetPath);
            return assetAttributes == null || assetAttributes.packer != AssetPacker.DontPack;
        }

        // 确定一个资源是否需要进入 StreamingAssets
        public bool IsStreamingAssets(string assetPath, BundleBuilderData.BundleInfo bundleInfo)
        {
            var assetAttributes = GetAssetPathAttributes(assetPath);

            // 配置为自动分配 StreamingAssets
            if (assetAttributes == null || assetAttributes.packer == AssetPacker.Auto)
            {
                // 出现在分析列表中 
                if (assetListData != null && assetListData.Contains(assetPath))
                {
                    return true;
                }

                // 继承主包属性
                return bundleInfo.streamingAssets;
            }

            return assetAttributes.packer == AssetPacker.AlwaysSA;
        }
    }

    public class ZipArchiveBuild
    {
        public string name;
        public List<string> assetPaths = new List<string>();
    }

    public class ZipArchiveManifestEntry
    {
        public string name;
        public List<string> assets = new List<string>();
    }

    public class ZipArchiveManifest
    {
        public List<ZipArchiveManifestEntry> archives = new List<ZipArchiveManifestEntry>();
    }

    public class FileListManifestEntry
    {
        public string name;
    }

    public class FileListManifestFileInfo
    {
        public string assetPath; // 记录了打包过程中需要复制的文件路径 (AssetPath)
        public bool streamingAssets; // 是否复制到 StreamingAssets
    }

    public class FileListManifest
    {
        public List<FileListManifestFileInfo> fileEntrys = new List<FileListManifestFileInfo>();
        public List<FileListManifestEntry> fileLists = new List<FileListManifestEntry>();
    }

    public class RawFileBuild
    {
        public string name;
        public string[] assetNames;
    }

    public class RawFileManifest
    {
    }

    public class SceneBundleBuild
    {
        public string name;
        public string scenePath;
    }
}