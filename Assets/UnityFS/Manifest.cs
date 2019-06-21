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
        // 资源包清单
        [Serializable]
        public class BundleInfo
        {
            public string name; // assetbundle name (fileinfo.name)
            public Dictionary<string, string> assets; // asset path (virtual path) => asset name 
            public string[] dependencies; // 依赖的 Assetbundle
        }

        [Serializable]
        public class FileInfo
        {
            public bool startup;    // 是否需要在启动前完成下载更新
            public int priority;    // 下载排队优先级
            public string name;     // 文件名
            public int size;        // 文件大小
            public string checksum; // 文件校验值
        }

        public Dictionary<string, FileInfo> files;     // 文件清单
        public Dictionary<string, BundleInfo> bundles; // Assetbundle 清单
    }
}
