using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 打包过程数据
    public class PackageSharedBuildInfo
    {
        public BundleBuilderData data;
        public string outputPath;
        public string tag;
    }
}