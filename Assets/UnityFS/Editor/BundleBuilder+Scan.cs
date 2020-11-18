using System;
using System.IO;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public partial class BundleBuilder
    {
        private static int BundleComparer(BundleBuilderData.BundleInfo a, BundleBuilderData.BundleInfo b)
        {
            // streamingAssets 优先
            if (a.streamingAssets == b.streamingAssets)
            {
                return a.buildOrder - b.buildOrder;
            }

            return a.streamingAssets ? -1 : 1;
        }

        // 根据 targets 遍历产生所有实际资源列表 assets
        public static bool Scan(BundleBuilderData data)
        {
            data.Cleanup();
            var bundles = data.bundles.ToArray();
            Array.Sort(bundles, BundleComparer);
            foreach (var bundle in bundles)
            {
                ScanBundle(data, bundle);
            }

            return true;
        }

        // 根据 targets 遍历产生所有实际资源列表 assets
        public static bool ScanBundle(BundleBuilderData data, BundleBuilderData.BundleInfo bundle)
        {
            if (!bundle.enabled)
            {
                return false;
            }

            foreach (var targetAsset in bundle.targets)
            {
                if (targetAsset.enabled/*&& targetAsset.IsBuildPlatform(buildPlatform)*/)
                {
                    Scan(data, bundle, targetAsset.targetPath, targetAsset.platform);
                }
            }

            if (bundle.Slice(data))
            {
                data.MarkAsDirty();
            }

            return true;
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, string targetPath, PackagePlatform platform)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                return;
            }

            if (Directory.Exists(targetPath))
            {
                // 是一个目录
                foreach (var directory in Directory.GetDirectories(targetPath))
                {
                    Scan(data, bundle, directory, platform);
                }

                foreach (var file in Directory.GetFiles(targetPath))
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }

                    if (bundle.type == Manifest.BundleType.AssetBundle)
                    {
                        var fi = new FileInfo(file);
                        if (data.skipExts.Contains(fi.Extension.ToLower()))
                        {
                            continue;
                        }
                    }

                    var normFileName = file.Replace('\\', '/');
                    // var fileAsset = AssetDatabase.LoadMainAssetAtPath(normFileName);
                    CollectAsset(data, bundle, normFileName, platform);
                }
            }
            else
            {
                CollectAsset(data, bundle, targetPath, platform);
            }
        }

        private static bool CollectAssetList(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, AssetListData assetListData, PackagePlatform platform)
        {
            if (assetListData == null)
            {
                return false;
            }

            for (var index = 0; index < assetListData.timestamps.Count; index++)
            {
                var ts = assetListData.timestamps[index];
                var assetPath = ts.assetPath;

                // 剔除 filelist 对象
                if (!Directory.Exists(assetPath))
                {
                    //TODO: 场景需要单独拆包
                    if (assetPath.EndsWith(".unity"))
                    {
                        continue;
                    }

                    // var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    CollectAsset(data, bundle, assetPath, platform);
                }
            }

            return true;
        }

        // 最终资源
        private static bool CollectAsset(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, string assetPath, PackagePlatform platform)
        {
            if (assetPath.EndsWith(Manifest.AssetListDataExt))
            {
                var listData = AssetListData.ReadFrom(assetPath);
                return CollectAssetList(data, bundle, listData, platform);
            }

            for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
            {
                var split = bundle.splits[splitIndex];
                var ruleMatch = false;
                if (split.rules.Count > 0)
                {
                    for (var ruleIndex = 0; ruleIndex < split.rules.Count; ruleIndex++)
                    {
                        var rule = split.rules[ruleIndex];
                        if (rule.exclude)
                        {
                            if (IsRuleMatched(rule, assetPath))
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (IsRuleMatched(rule, assetPath))
                            {
                                ruleMatch = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ruleMatch = true;
                }

                if (ruleMatch)
                {
                    if (!ContainsAsset(data, assetPath) && split.AddObject(assetPath, platform))
                    {
                        CheckShaderVariants(data, bundle, assetPath, platform);
                    }

                    return true;
                }
            }

            return false;
        }

        private static void CheckShaderVariants(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, string assetPath, PackagePlatform platform)
        {
#if UNITY_2018_1_OR_NEWER
            if (!data.extractShaderVariantCollections)
            {
                return;
            }

            if (!assetPath.EndsWith(".shadervariants"))
            {
                return;
            }

            var shaderVariants = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var shaderVariantCollection = shaderVariants as ShaderVariantCollection;
            if (shaderVariantCollection != null)
            {
                var shaderInfos = ShaderUtil.GetAllShaderInfo();
                foreach (var shaderInfo in shaderInfos)
                {
                    var shader = Shader.Find(shaderInfo.name);
                    var shaderPath = AssetDatabase.GetAssetPath(shader);
                    if (shaderPath.StartsWith("Assets/"))
                    {
                        //TODO: check if in shaderVariants

                        CollectAsset(data, bundle, shaderPath, platform);
                    }
                }
            }
#endif
        }

        public static bool ContainsAsset(BundleBuilderData data, string assetPath)
        {
            foreach (var bundle in data.bundles)
            {
                foreach (var split in bundle.splits)
                {
                    if (split.ContainsAssetPath(assetPath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTextureAsset(string extension)
        {
            return extension == ".png"
                || extension == ".jpg" || extension == ".jpeg"
                || extension == ".bmp"
                || extension == ".tga";
        }

        private static bool IsTextAsset(string extension)
        {
            return extension == ".txt"
                || extension == ".bytes"
                || extension == ".xml"
                || extension == ".json";
        }

        private static bool IsAudioAsset(string extension)
        {
            return extension == ".mp3"
                || extension == ".ogg";
        }

        public static bool IsAssetTypeMatched(BundleBuilderData.BundleSplitRule rule, string assetPath, FileInfo fileInfo)
        {
            if (rule.assetTypes != 0)
            {
                var ext = fileInfo.Extension.ToLower();
                if ((rule.assetTypes & BundleAssetTypes.Prefab) == 0 && ext == ".prefab")
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.TextAsset) == 0 && IsTextAsset(ext))
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Animation) == 0 && ext == ".anim")
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Material) == 0 && ext == ".mat")
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Texture) == 0 && IsTextureAsset(ext))
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Audio) == 0 && IsAudioAsset(ext))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsRuleMatched(BundleBuilderData.BundleSplitRule rule, string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            var assetName = fileInfo.Name.Substring(0, fileInfo.Name.Length - fileInfo.Extension.Length);

            switch (rule.type)
            {
                case BundleBuilderData.BundleSplitType.Prefix:
                    {
                        if (!assetName.StartsWith(rule.keyword))
                        {
                            return false;
                        }

                        break;
                    }
                case BundleBuilderData.BundleSplitType.Suffix:
                    {
                        if (!assetName.EndsWith(rule.keyword))
                        {
                            return false;
                        }

                        break;
                    }
                case BundleBuilderData.BundleSplitType.FileSuffix:
                    {
                        if (!assetPath.EndsWith(rule.keyword))
                        {
                            return false;
                        }

                        break;
                    }
                case BundleBuilderData.BundleSplitType.PathPrefix:
                    {
                        if (!assetPath.StartsWith(rule.keyword))
                        {
                            return false;
                        }

                        break;
                    }
            }

            return IsAssetTypeMatched(rule, assetPath, fileInfo);
        }
    }
}