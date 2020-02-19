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

        public class BundleSlice
        {
            public string name;
            public List<Object> assets = new List<Object>(); // 最终进入打包的所有资源对象
        }

        [Serializable]
        public class BundleSplit
        {
            public string name = string.Empty; // 分包名
            public int sliceObjects;
            public List<BundleSplitRule> rules = new List<BundleSplitRule>();

            [NonSerialized]
            public List<Object> assets = new List<Object>();
            [NonSerialized]
            public List<BundleSlice> slices = new List<BundleSlice>();

            public void Slice(string bundleName)
            {
                foreach (var asset in assets)
                {
                    GetBundleSlice(bundleName).assets.Add(asset);
                }
            }

            public static string GetBundleName(string name, string part)
            {
                if (string.IsNullOrEmpty(part))
                {
                    return name;
                }
                var dot = name.LastIndexOf('.');
                string prefix;
                string suffix;
                if (dot >= 0)
                {
                    prefix = name.Substring(0, dot);
                    suffix = name.Substring(dot);
                }
                else
                {
                    prefix = name;
                    suffix = string.Empty;
                }
                return prefix + "_" + part + suffix;
            }

            public BundleSlice GetBundleSlice(string bundleName)
            {
                var splitName = this.name ?? string.Empty;
                var count = this.slices.Count;
                var slice = count > 0 ? this.slices[count - 1] : null;
                if (slice == null || this.sliceObjects >= 1 && slice.assets.Count >= this.sliceObjects)
                {
                    slice = new BundleSlice();
                    this.slices.Add(slice);
                    var baseName = splitName;
                    if (this.sliceObjects != 0 && count != 0)
                    {
                        baseName += "_" + count;
                    }
                    slice.name = GetBundleName(bundleName, baseName).ToLower();
                }
                return slice;
            }
        }

        [Serializable]
        public class BundleSplitRule
        {
            public BundleSplitType type;
            public BundleAssetTypes assetTypes;
            public string keyword;
            public bool exclude;

            [NonSerialized]
            public List<Object> assets = new List<Object>();
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

            public List<string> assetsGuidOrder = new List<string>();

            public List<BundleSplit> splits = new List<BundleSplit>();

            private static int Neg2Inf(int v)
            {
                return v < 0 ? int.MaxValue : v;
            }

            public static string GetAssetGUID(Object asset)
            {
                var assetPath = AssetDatabase.GetAssetOrScenePath(asset);
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                return guid;
            }

            public bool AddAssetOrder(Object asset)
            {
                var guid = GetAssetGUID(asset);
                if (!assetsGuidOrder.Contains(guid))
                {
                    assetsGuidOrder.Add(guid);
                    return true;
                }
                return false;
            }

            public void Slice()
            {
                foreach (var split in splits)
                {
                    split.assets.Sort((a, b) =>
                    {
                        var da = GetAssetGUID(a);
                        var db = GetAssetGUID(b);
                        return Neg2Inf(assetsGuidOrder.IndexOf(da)) - Neg2Inf(assetsGuidOrder.IndexOf(db));
                    });
                    split.Slice(name);
                }
            }

            public void Cleanup()
            {
                foreach (var split in splits)
                {
                    split.assets.Clear();
                    split.slices.Clear();
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

    public class ZipArchiveEntry
    {
        public string name;
        public List<string> assets = new List<string>();
    }

    public class ZipArchiveManifest
    {
        public List<ZipArchiveEntry> archives = new List<ZipArchiveEntry>();
    }

    public class SceneBundleBuild
    {
        public string name;
        public string scenePath;
    }
}
