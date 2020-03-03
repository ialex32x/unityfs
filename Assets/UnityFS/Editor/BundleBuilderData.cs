using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderData
    : ScriptableObject
    {
        public const string BundleBuilderDataPath = "Assets/unityfs.asset";
        public const string Ext = ".pkg";

        [Serializable]
        public class BundleAssetTarget
        {
            public int id;
            public bool enabled = true;
            public Object target;
            // public BundleAssetTypes types = (BundleAssetTypes)~0; // (仅搜索目录时) 仅包含指定资源类型
            // public List<string> extensions = new List<string>();  // (仅搜索目录时) 额外包含指定后缀的文件
        }

        [Serializable]
        public enum BundleSplitType
        {
            None,
            Prefix,
            Suffix,
        }

        [Serializable]
        public class BundleSlice
        {
            public string name;
            public int capacity;

            // 最终进入打包的所有资源对象
            private List<string> _assetGuids;

            public List<string> assetGuids
            {
                get
                {
                    if (_assetGuids == null)
                    {
                        _assetGuids = new List<string>();
                    }
                    return _assetGuids;
                }
            }

            public List<string> histroy = new List<string>();

            public BundleSlice(string name, int cap)
            {
                this.name = name;
                this.capacity = cap;
            }

            public void Cleanup()
            {
                assetGuids.Clear();
            }

            // 如果是历史资源, 将加入; 否则返回 false
            public bool AddHistory(string guid)
            {
                if (histroy.Contains(guid))
                {
                    assetGuids.Add(guid);
                    return true;
                }
                return false;
            }

            // 尝试添加资源
            // 仅历史数量在切分容量剩余时可以加入
            public bool AddNew(string guid)
            {
                if (capacity <= 0 || histroy.Count < capacity)
                {
                    assetGuids.Add(guid);
                    histroy.Add(guid);
                    return true;
                }
                return false;
            }
        }

        [Serializable]
        public class BundleSplit
        {
            public string name = string.Empty; // 分包名
            public int sliceObjects;
            public List<BundleSplitRule> rules = new List<BundleSplitRule>();

            // scan 过程收集将要打入此 split 的所有资源的列表
            [NonSerialized]
            public List<Object> assets = new List<Object>();

            public List<BundleSlice> slices = new List<BundleSlice>();

            public void Cleanup()
            {
                assets.Clear();
                foreach (var slice in slices)
                {
                    slice.Cleanup();
                }
            }

            public bool Slice(string bundleName)
            {
                var dirty = false;
                foreach (var asset in assets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (AdjustBundleSlice(bundleName, guid))
                    {
                        dirty = true;
                    }
                }
                return dirty;
            }

            // 将 slice 切分命名插入 split 命名与文件后缀名之间
            public string GetBundleName(string bundleName)
            {
                var baseName = this.name ?? string.Empty;
                if (this.sliceObjects != 0 && this.slices.Count != 0)
                {
                    baseName = "_" + baseName + "_" + this.slices.Count;
                }
                if (string.IsNullOrEmpty(baseName))
                {
                    return bundleName;
                }
                var dot = bundleName.LastIndexOf('.');
                string prefix;
                string suffix;
                if (dot >= 0)
                {
                    prefix = bundleName.Substring(0, dot);
                    suffix = bundleName.Substring(dot);
                }
                else
                {
                    prefix = bundleName;
                    suffix = string.Empty;
                }
                return prefix + baseName + suffix;
            }

            public bool AdjustBundleSlice(string bundleName, string guid)
            {
                for (var i = 0; i < this.slices.Count; i++)
                {
                    var oldSlice = this.slices[i];
                    if (oldSlice.AddHistory(guid))
                    {
                        return false;
                    }
                }
                var count = this.slices.Count;
                var lastSlice = count > 0 ? this.slices[count - 1] : null;
                if (lastSlice == null || !lastSlice.AddNew(guid))
                {
                    var sliceName = GetBundleName(bundleName).ToLower();
                    var newSlice = new BundleSlice(sliceName, this.sliceObjects);
                    this.slices.Add(newSlice);
                    newSlice.AddNew(guid);
                }
                return true;
            }
        }

        [Serializable]
        public class BundleSplitRule
        {
            public BundleSplitType type;
            public BundleAssetTypes assetTypes;
            public string keyword;
            public bool exclude;
        }

        public class Variable
        {
            public string name;

            public int intValue;
            public string stringValue;
        }

        [Serializable]
        public class BundleInfo
        {
            public int id;
            public string name; // bundle filename
            public string note;
            public Manifest.BundleType type;
            public Manifest.BundleLoad load;
            public BundleAssetPlatforms platforms = (BundleAssetPlatforms)~0;  // filter for platforms
            public bool enabled = true;
            public bool streamingAssets = false; // 是否复制到 StreamingAssets 目录
            public int priority;
            public List<BundleAssetTarget> targets = new List<BundleAssetTarget>(); // 打包目标 (可包含文件夹)

            public List<BundleSplit> splits = new List<BundleSplit>();

            public static string GetAssetGUID(Object asset)
            {
                var assetPath = AssetDatabase.GetAssetOrScenePath(asset);
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                return guid;
            }

            public bool Slice()
            {
                var dirty = false;
                foreach (var split in splits)
                {
                    if (split.Slice(name))
                    {
                        dirty = true;
                    }
                }
                return dirty;
            }

            public void Cleanup()
            {
                foreach (var split in splits)
                {
                    split.Cleanup();
                }
            }
        }

        public int id;
        public List<BundleInfo> bundles = new List<BundleInfo>();

        public static BundleBuilderData Load()
        {
            // var data = new BundleBuilderData();
            // EditorJsonUtility.FromJsonOverwrite(BundleBuilderDataPath, data);

            var data = AssetDatabase.LoadMainAssetAtPath(BundleBuilderDataPath) as BundleBuilderData;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BundleBuilderData>();
                AssetDatabase.CreateAsset(data, BundleBuilderDataPath);
                AssetDatabase.SaveAssets();
            }

            return data;
        }

        public void MarkAsDirty()
        {
            EditorUtility.SetDirty(this);

            // var json = EditorJsonUtility.ToJson(this, true);
            // File.WriteAllText(BundleBuilderDataPath, json);
        }

        public void Cleanup()
        {
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
    }

    public class ZipArchiveBuildEntry
    {
        public string name;
        public List<string> assets = new List<string>();
    }

    public class ZipArchiveBuild
    {
        public List<ZipArchiveBuildEntry> archives = new List<ZipArchiveBuildEntry>();
    }

    public class FileListBuildEntry
    {
        public string name;
    }

    public class FileListBuild
    {
        public List<string> fileEntrys = new List<string>(); // 记录了打包过程中需要复制的文件路径 (AssetPath)
        public List<FileListBuildEntry> fileLists = new List<FileListBuildEntry>();
    }

    public class SceneBundleBuild
    {
        public string name;
        public string scenePath;
    }
}
