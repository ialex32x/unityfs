using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 清单
    [Serializable]
    public class Manifest
    {
        public enum BundleType
        {
            AssetBundle,
            ZipArchive,
            // SceneBundle,
        }

        // 资源包清单
        [Serializable]
        public class BundleInfo
        {
            public BundleType type; // 资源包类型
            public bool startup;    // 是否需要在启动前完成下载更新
            public int priority;    // 下载排队优先级
            public string name;     // 文件名
            public int size;        // 文件大小
            public string checksum; // 文件校验值
            public string[] dependencies; // 依赖的 bundle
            public List<string> assets = new List<string>(); // asset path (virtual path)
        }

        public List<BundleInfo> bundles = new List<BundleInfo>(); // bundle 清单
    }
}
