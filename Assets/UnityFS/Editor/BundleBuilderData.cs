using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;

    public class BundleBuilderData : ScriptableObject
    {
        [Serializable]
        public class BundleAssetTarget
        {
            public int id;
            public bool enabled = true;
            public Object target;
            public BundleAssetPlatforms platforms = ~BundleAssetPlatforms.None;  // filter for platforms
            public BundleAssetTypes types = ~BundleAssetTypes.None; // (仅搜索目录时) 仅包含指定资源类型
            public string extensions = string.Empty;    // (仅搜索目录时) 额外包含指定后缀的文件
        }

        [Serializable]
        public class BundleAsset
        {
            public int id;
            public Object target;
            public int splitIndex;
        }

        [Serializable]
        public class BundleInfo
        {
            public int id;
            public string name; // bundle filename
            public string note;
            public Manifest.BundleType type;
            public bool enabled = true;

            public BundleLoad load;
            public int priority;
            public int splitObjects; // 自动分包
            public List<BundleAssetTarget> targets = new List<BundleAssetTarget>(); // 打包目标 (可包含文件夹)

            [NonSerialized]
            public List<BundleAsset> assets = new List<BundleAsset>(); // 最终进入打包的所有资源对象
        }

        public int id;
        public List<BundleInfo> bundles = new List<BundleInfo>();
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
