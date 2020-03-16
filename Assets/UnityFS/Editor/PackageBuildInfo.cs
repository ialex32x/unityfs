using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 打包过程数据
    public class PackageBuildInfo
    {
        private BundleBuilderData _data;
        private BuildTarget _buildTarget;

        // 分平台
        private string _assetBundlePath; // 原始 assetbundle 输出目录
        private string _zipArchivePath; // zip 压缩包输出目录 
        private string _packagePath; // 最终包输出目录

        public BundleBuilderData data => _data;

        public BuildTarget buildTarget => _buildTarget;

        public string assetBundlePath => _assetBundlePath;

        public string zipArchivePath => _zipArchivePath;

        public string packagePath => _packagePath;

        // 输出文件收集
        public List<string> filelist = new List<string>();

        // outputPath: 输出的总目录 [可选]
        public PackageBuildInfo(BundleBuilderData data, string outputPath, BuildTarget buildTarget)
        {
            _data = data;
            _buildTarget = buildTarget;
            _assetBundlePath = GetPlatformPath(Combine(outputPath, data.assetBundlePath), buildTarget);
            _zipArchivePath = GetPlatformPath(Combine(outputPath, data.zipArchivePath), buildTarget);
            _packagePath = GetPlatformPath(Combine(outputPath, data.packagePath), buildTarget);

            EnsureDirectory(_assetBundlePath);
            EnsureDirectory(_zipArchivePath);
            EnsureDirectory(_packagePath);
        }

        private static string Combine(string part1, string part2)
        {
            return string.IsNullOrEmpty(part1) ? part2 : Path.Combine(part1, part2);
        }

        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string GetPlatformPath(string basePath, BuildTarget buildTarget)
        {
            return Path.Combine(basePath, Utils.Helpers.GetBuildTargetName(buildTarget));
        }
    }
}