using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;

    public class BundleBuilderData : ScriptableObject
    {
        [Serializable]
        public class BundleAsset
        {
            public Object target;
            public BundleAssetPlatforms platforms;  // filter for platforms
            public BundleAssetTypes types;          // filter for directory object
            public int splitIndex;
        }

        [Serializable]
        public class BundleInfo
        {
            public bool startup;
            public int priority;
            public string name;      // bundle filename
            public int splitObjects; // 自动分包
            public BundleType type;
            public List<BundleAsset> assets = new List<BundleAsset>();
        }

        public List<BundleInfo> bundles = new List<BundleInfo>();
    }
}
