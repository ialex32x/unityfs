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

            public Object target;

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
        public string encryptionKey;
        public List<BundleInfo> bundles = new List<BundleInfo>();
        [SerializeField]
        private AssetAttributesMap assetAttributesMap = new AssetAttributesMap();
        public string assetBundlePath = "out/bundles";
        public string zipArchivePath = "out/zipArchives";
        public string packagePath = "out/packages";
        public int priorityMax = 10000;
        public int searchMax = 200;
        public AssetListData assetListData;

        [NonSerialized] public List<Object> allCollectedAssets = new List<Object>();

        [NonSerialized] public string[] allCollectedAssetsPath = new string[0];

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

        public AssetAttributes AddAssetAttributes(string guid)
        {
            AssetAttributes attrs;
            if (!assetAttributesMap.TryGetValue(guid, out attrs))
            {
                attrs = assetAttributesMap[guid] = new AssetAttributes();
                MarkAsDirty();   
            }
            return attrs;
        }

        public bool RemoveAssetAttributes(string guid)
        {
            if (assetAttributesMap.Remove(guid))
            {
                MarkAsDirty();
                return true;
            }

            return false;
        }

        public AssetAttributes GetAssetAttributes(string guid)
        {
            AssetAttributes attrs;
            return assetAttributesMap.TryGetValue(guid, out attrs) ? attrs : null;
        }

        public void OnAssetCollect(Object asset, string assetPath)
        {
            allCollectedAssets.Add(asset);
            ArrayUtility.Add(ref allCollectedAssetsPath, assetPath);
            MarkAsDirty();
        }

        public void MarkAsDirty()
        {
            EditorUtility.SetDirty(this);
        }

        public void Cleanup()
        {
            allCollectedAssets.Clear();
            ArrayUtility.Clear(ref allCollectedAssetsPath);
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

        // 确定一个资源是否需要进入 StreamingAssets
        public bool IsStreamingAssets(string guid, BundleBuilderData.BundleInfo bundleInfo)
        {
            var assetAttributes = GetAssetAttributes(guid);
            
            // 配置为自动分配 StreamingAssets
            if (assetAttributes == null || assetAttributes.packer == AssetPacker.Auto)
            {
                // 出现在分析列表中 
                if (assetListData != null && assetListData.Contains(guid))
                {
                    return true;
                }

                // 继承主包属性
                return bundleInfo.streamingAssets;
            }

            return assetAttributes.packer == AssetPacker.Always;
        }
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

    public class ZipArchiveBuild
    {
        public string name;
        public List<string> assetPaths = new List<string>();
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

    public class SceneBundleBuild
    {
        public string name;
        public string scenePath;
    }
}