using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 打包过程数据
    public partial class PackageBuildInfo
    {
        private PackageSharedBuildInfo _sharedBuildInfo;
        private BundleBuilderData _data;
        private BuildTarget _buildTarget;

        // 分平台
        private string _assetBundlePath; // 原始 assetbundle 输出目录
        private string _zipArchivePath; // zip 压缩包输出目录 
        private string _packagePath; // 最终包输出目录
        private string _streamingAssetsPath; // 最终 StreamingAssets 输出目录

        public BundleBuilderData data => _data;

        public PackageSharedBuildInfo sharedBuildInfo => _sharedBuildInfo;

        public BuildTarget buildTarget => _buildTarget;

        public string assetBundlePath => _assetBundlePath;

        public string zipArchivePath => _zipArchivePath;

        public string packagePath => _packagePath;

        public string streamingAssetsPath => _streamingAssetsPath;

        // 输出文件收集
        public List<string> filelist = new List<string>();

        // outputPath: 输出的总目录 [可选]
        public PackageBuildInfo(PackageSharedBuildInfo sharedBuildInfo, BuildTarget buildTarget)
        {
            _sharedBuildInfo = sharedBuildInfo;
            _data = sharedBuildInfo.data;
            _buildTarget = buildTarget;
            _assetBundlePath = GetPlatformPath(Combine(sharedBuildInfo.outputPath, data.assetBundlePath), buildTarget);
            _zipArchivePath = GetPlatformPath(Combine(sharedBuildInfo.outputPath, data.zipArchivePath), buildTarget);
            _packagePath = GetPlatformPath(Combine(sharedBuildInfo.outputPath, data.packagePath), buildTarget);
            _streamingAssetsPath = Combine(BundleBuilderData.StreamingAssetsPath, Manifest.EmbeddedBundlesBasePath);

            EnsureDirectory(_assetBundlePath);
            EnsureDirectory(_zipArchivePath);
            EnsureDirectory(_packagePath);
            EnsureDirectory(_streamingAssetsPath);
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