using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;
using NUnit.Framework;
using UnityEditor.WindowsStandalone;

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
                    Scan(data, bundle, targetAsset.target, targetAsset.platform);
                }
            }

            if (bundle.Slice(data))
            {
                data.MarkAsDirty();
            }

            return true;
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, Object asset, PackagePlatform platform)
        {
            if (asset == null)
            {
                return;
            }

            var targetPath = AssetDatabase.GetAssetPath(asset);
            if (Directory.Exists(targetPath))
            {
                // 是一个目录
                foreach (var directory in Directory.GetDirectories(targetPath))
                {
                    Scan(data, bundle, AssetDatabase.LoadMainAssetAtPath(directory), platform);
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
                    var fileAsset = AssetDatabase.LoadMainAssetAtPath(normFileName);
                    CollectAsset(data, bundle, fileAsset, normFileName, platform);
                }
            }
            else
            {
                CollectAsset(data, bundle, asset, targetPath, platform);
            }
        }

        private static bool CollectAssetList(BundleBuilderData data, BundleBuilderData.BundleInfo bundle,
            AssetListData asset, PackagePlatform platform)
        {
            for (var index = 0; index < asset.timestamps.Count; index++)
            {
                var ts = asset.timestamps[index];
                var assetPath = AssetDatabase.GUIDToAssetPath(ts.guid);

                // 剔除 filelist 对象
                if (!Directory.Exists(assetPath))
                {
                    //TODO: 场景需要单独拆包
                    if (assetPath.EndsWith(".unity"))
                    {
                        continue;
                    }

                    var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    CollectAsset(data, bundle, mainAsset, assetPath, platform);
                }
            }

            return true;
        }

        // 最终资源
        private static bool CollectAsset(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, Object asset,
            string assetPath, PackagePlatform platform)
        {
            if (asset == null)
            {
                return false;
            }

            var listData = asset as AssetListData;
            if (listData != null)
            {
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
                            if (IsRuleMatched(rule, asset, assetPath))
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (IsRuleMatched(rule, asset, assetPath))
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
                    if (!ContainsAsset(data, asset) && split.AddObject(asset, platform))
                    {
                        data.OnAssetCollect(asset, assetPath);
                    }

                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAsset(BundleBuilderData data, Object assetObject)
        {
            foreach (var bundle in data.bundles)
            {
                foreach (var split in bundle.splits)
                {
                    if (split.ContainsObject(assetObject))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsAssetTypeMatched(BundleBuilderData.BundleSplitRule rule, Object asset)
        {
            if (rule.assetTypes != 0)
            {
                if ((rule.assetTypes & BundleAssetTypes.Prefab) == 0 && asset is GameObject)
                {
                    var file = AssetDatabase.GetAssetPath(asset);
                    if (file != null && file.EndsWith(".prefab"))
                    {
                        return false;
                    }
                }
                else if ((rule.assetTypes & BundleAssetTypes.TextAsset) == 0 && asset is TextAsset)
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Animation) == 0 &&
                         (asset is Animation || asset is AnimationClip))
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Material) == 0 && asset is Material)
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Texture) == 0 && asset is Texture)
                {
                    return false;
                }
                else if ((rule.assetTypes & BundleAssetTypes.Audio) == 0 && asset is AudioClip)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsRuleMatched(BundleBuilderData.BundleSplitRule rule, Object asset, string assetPath)
        {
            switch (rule.type)
            {
                case BundleBuilderData.BundleSplitType.Prefix:
                {
                    if (!asset.name.StartsWith(rule.keyword))
                    {
                        return false;
                    }

                    break;
                }
                case BundleBuilderData.BundleSplitType.Suffix:
                {
                    if (!asset.name.EndsWith(rule.keyword))
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

            return IsAssetTypeMatched(rule, asset);
        }
    }
}