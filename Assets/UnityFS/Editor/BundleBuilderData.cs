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
            public BundleAssetPlatforms platforms = BundleAssetPlatforms.Any;  // filter for platforms
            public BundleAssetTypes types = BundleAssetTypes.Any;          // filter for directory object
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
            public string name;      // bundle filename
            public BundleType type;
            public bool enabled = true;

            public BundleLoad load;
            public int priority;
            public int splitObjects; // 自动分包
            public List<BundleAssetTarget> targets = new List<BundleAssetTarget>(); // 打包目标 (可包含文件夹)

            public List<BundleAsset> assets = new List<BundleAsset>(); // 最终进入打包的所有资源对象
        }

        public int id;
        public List<BundleInfo> bundles = new List<BundleInfo>();
    }
}
