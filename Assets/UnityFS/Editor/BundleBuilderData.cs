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
            public BundleAssetTypes types = (BundleAssetTypes)~0; // (仅搜索目录时) 仅包含指定资源类型
            public string extensions = string.Empty;    // (仅搜索目录时) 额外包含指定后缀的文件
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
            public string fullName;
            public List<Object> assets = new List<Object>(); // 最终进入打包的所有资源对象
        }

        public class BundleSplit
        {
            public string name; // 分包名
            public List<BundleSlice> slices = new List<BundleSlice>();
        }

        [Serializable]
        public class BundleSplitRule
        {
            public BundleSplitType type;
            public string keyword;
            public string name;
            public int capacity;
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

            public List<Variable> variables = new List<Variable>();

            public List<BundleSplitRule> rules = new List<BundleSplitRule>();

            [NonSerialized]
            public List<Object> assetsCache = new List<Object>();

            public List<Object> assetsOrder = new List<Object>();

            [NonSerialized]
            public List<BundleSplit> splits = new List<BundleSplit>();

            //TODO: 用规则代替
            public int splitObjects; // 自动分包

            public Variable GetVariable(string name)
            {
                for (int i = 0, size = variables.Count; i < size; i++)
                {
                    var v = variables[i];
                    if (v.name == name)
                    {
                        return v;
                    }
                }
                var n = new Variable();
                n.name = name;
                variables.Add(n);
                return n;
            }

            private BundleSplit GetBundleSplit(string name)
            {
                for (int i = 0, size = splits.Count; i < size; i++)
                {
                    var v = splits[i];
                    if (v.name == name)
                    {
                        return v;
                    }
                }
                var n = new BundleSplit();
                n.name = name;
                splits.Add(n);
                return n;
            }

            public BundleSlice GetBundleSlice(string name)
            {
                var split = GetBundleSplit(name);
                var count = split.slices.Count;
                var slice = count > 0 ? split.slices[count - 1] : null;
                if (slice == null || this.splitObjects >= 1 && slice.assets.Count >= this.splitObjects)
                {
                    slice = new BundleSlice();
                    split.slices.Add(slice);
                    var dot = this.name.LastIndexOf('.');
                    string prefix;
                    string suffix;

                    if (dot >= 0)
                    {
                        prefix = this.name.Substring(0, dot);
                        suffix = this.name.Substring(dot);
                    }
                    else
                    {
                        prefix = this.name;
                        suffix = string.Empty;
                    }

                    if (this.splitObjects == 0 || count == 0)
                    {
                        if (string.IsNullOrEmpty(split.name))
                        {
                            slice.fullName = this.name;
                        }
                        else
                        {
                            slice.fullName = prefix + "_" + split.name + suffix;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(split.name))
                        {
                            slice.fullName = prefix + "_" + count + suffix;
                        }
                        else
                        {
                            slice.fullName = prefix + "_" + split.name + "_" + count + suffix;
                        }
                    }
                }
                return slice;
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
    }

    public class AssetFilter
    {
        public string[] extensions;
        public BundleAssetTypes types;
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
