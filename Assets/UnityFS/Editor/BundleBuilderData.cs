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
            Counter,
            Prefix,
        }

        public class BundleSplit
        {
            public string name; // 分包名
            public BundleSplitType type; // 分包方式
            public List<Object> assets = new List<Object>(); // 最终进入打包的所有资源对象
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

            public BundleSplitType splitType;
            public List<Variable> variables = new List<Variable>();

            [NonSerialized]
            public List<BundleSplit> splits = new List<BundleSplit>();

            // legacy
            // public int splitObjects; // 自动分包

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
        public int size;
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
