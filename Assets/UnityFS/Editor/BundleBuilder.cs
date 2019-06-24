using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilder
    {
        public const string BundleBuilderDataPath = "Assets/UnityFS/Data/default.asset";

        private static BundleBuilderData _data;

        public static BundleBuilderData GetData()
        {
            if (_data == null)
            {
                _data = AssetDatabase.LoadMainAssetAtPath(BundleBuilderDataPath) as BundleBuilderData;
                if (_data == null)
                {
                    _data = ScriptableObject.CreateInstance<BundleBuilderData>();
                    AssetDatabase.CreateAsset(_data, BundleBuilderDataPath);
                    AssetDatabase.SaveAssets();
                }
                var dirty = false;
                foreach (var bundle in _data.bundles)
                {
                    if (bundle.id == 0)
                    {
                        bundle.id = ++_data.id;
                        dirty = true;
                    }
                    foreach (var target in bundle.targets)
                    {
                        if (target.id == 0)
                        {
                            target.id = ++_data.id;
                            dirty = true;
                        }
                    }
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(_data);
                }
            }
            return _data;
        }

        public static bool Scan(BundleBuilderData.BundleInfo bundle)
        {
            var assets = new List<Object>();
            foreach (var target in bundle.targets)
            {
                Scan(bundle, target, assets);
            }
            // diff assets and bundle.assets
            bundle.assets.Clear();
            foreach (var asset in assets)
            {
                bundle.assets.Add(new BundleBuilderData.BundleAsset()
                {
                    target = asset,
                });
            }
            return true;
        }

        public static void Scan(BundleBuilderData.BundleInfo bundle, BundleBuilderData.BundleAssetTarget target, List<Object> assets)
        {
            //TODO: 过滤条件
            Scan(bundle, target.target, assets);
        }

        public static void Scan(BundleBuilderData.BundleInfo bundle, Object target, List<Object> assets)
        {
            if (target == null)
            {
                return;
            }
            var targetPath = AssetDatabase.GetAssetPath(target);
            if (Directory.Exists(targetPath))
            {
                // 是一个目录
                foreach (var directory in Directory.GetDirectories(targetPath))
                {
                    Scan(bundle, AssetDatabase.LoadMainAssetAtPath(directory), assets);
                }
                foreach (var file in Directory.GetFiles(targetPath))
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }
                    Scan(bundle, AssetDatabase.LoadMainAssetAtPath(file), assets);
                }
            }
            else
            {
                assets.Add(target);
            }
        }

        public static List<AssetBundleBuild> GenerateAssetBundleBuilds(BundleBuilderData data)
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type != BundleType.AssetBundle)
                {
                    continue;
                }
                var build = new AssetBundleBuild();
                build.assetBundleName = bundle.name;
                var assetNames = new List<string>();
                var assetPaths = new List<string>();
                foreach (var asset in bundle.assets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset.target);
                    assetNames.Add(assetPath);
                    assetPaths.Add(assetPath);
                }
                build.assetNames = assetNames.ToArray();
                build.addressableNames = assetPaths.ToArray();
                builds.Add(build);
            }
            return builds;
        }

        public static bool Scan(BundleBuilderData data)
        {
            var dirty = false;
            foreach (var bundle in data.bundles)
            {
                if (Scan(bundle))
                {
                    dirty = true;
                }
            }
            return dirty;
        }

        public static void Build(BundleBuilderData data, string outputPath, BuildTarget targetPlatform)
        {
            var builds = GenerateAssetBundleBuilds(data);
            if (builds.Count == 0)
            {
                Debug.LogWarning("no assetbundle to build");
                return;
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            var assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), BuildAssetBundleOptions.None, targetPlatform);
            // BuildPipeline.BuildPlayer()
            BuildManifest(data, outputPath, assetBundleManifest);
            Debug.Log($"build bundles finished {DateTime.Now}");
        }

        public static BundleBuilderData.BundleInfo GetBundleInfo(BundleBuilderData data, string bundleName)
        {
            foreach (var bundle in data.bundles)
            {
                if (bundle.name == bundleName)
                {
                    return bundle;
                }
            }
            return null;
        }

        public static void BuildManifest(BundleBuilderData data, string outputPath, AssetBundleManifest assetBundleManifest)
        {
            var assetBundles = assetBundleManifest.GetAllAssetBundles();
            var manifest = new Manifest();
            foreach (var assetBundle in assetBundles)
            {
                var bundleInfo = GetBundleInfo(data, assetBundle);
                var assetBundlePath = Path.Combine(outputPath, assetBundle);
                using (var stream = File.OpenRead(assetBundlePath))
                {
                    var fileInfo = new FileInfo(assetBundlePath);
                    var checksum = new Utils.Crc16();
                    checksum.Update(stream);
                    var bundle = new Manifest.BundleInfo();
                    bundle.name = assetBundle;
                    bundle.checksum = checksum.hex;
                    bundle.size = (int)fileInfo.Length;
                    bundle.startup = bundleInfo.load == BundleLoad.Startup;
                    bundle.priority = bundleInfo.priority;
                    foreach (var asset in bundleInfo.assets)
                    {
                        var assetPath = AssetDatabase.GetAssetPath(asset.target);
                        bundle.assets.Add(assetPath);
                    }
                    bundle.dependencies = assetBundleManifest.GetAllDependencies(assetBundle);
                    manifest.bundles.Add(bundle);
                }
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            var json = JsonUtility.ToJson(manifest);
            var manifestPath = Path.Combine(outputPath, "manifest.json");
            File.WriteAllText(manifestPath, json);
        }

        public static bool Contains(BundleBuilderData.BundleInfo bundleInfo, Object targetObject)
        {
            foreach (var target in bundleInfo.targets)
            {
                if (target.target == targetObject)
                {
                    return true;
                }
            }
            return false;
        }

        public static void Add(BundleBuilderData data, BundleBuilderData.BundleInfo bundleInfo, Object[] targetObjects)
        {
            foreach (var targetObject in targetObjects)
            {
                if (!Contains(bundleInfo, targetObject))
                {
                    bundleInfo.targets.Add(new BundleBuilderData.BundleAssetTarget()
                    {
                        id = ++data.id,
                        target = targetObject,
                    });
                }
            }
            EditorUtility.SetDirty(data);
        }
    }
}
