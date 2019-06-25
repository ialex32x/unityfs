using System;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

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

        // 根据 targets 遍历产生所有实际资源列表 assets
        public static bool ScanBundle(BundleBuilderData data, BundleBuilderData.BundleInfo bundle)
        {
            foreach (var target in bundle.targets)
            {
                Scan(data, bundle, target);
            }
            return true;
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, BundleBuilderData.BundleAssetTarget target)
        {
            //TODO: 过滤条件
            Scan(data, bundle, target.target);
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, Object target)
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
                    Scan(data, bundle, AssetDatabase.LoadMainAssetAtPath(directory));
                }
                foreach (var file in Directory.GetFiles(targetPath))
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }
                    Scan(data, bundle, AssetDatabase.LoadMainAssetAtPath(file));
                }
            }
            else
            {
                if (!ContainsAsset(data, target))
                {
                    bundle.assets.Add(new BundleBuilderData.BundleAsset()
                    {
                        target = target,
                    });
                }
            }
        }

        public static AssetBundleBuild[] GenerateAssetBundleBuilds(BundleBuilderData data)
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
            return builds.ToArray();
        }

        // 根据 targets 遍历产生所有实际资源列表 assets
        public static bool Scan(BundleBuilderData data)
        {
            foreach (var bundle in data.bundles)
            {
                bundle.assets.Clear();
            }
            foreach (var bundle in data.bundles)
            {
                ScanBundle(data, bundle);
            }
            return true;
        }

        // 生成打包 
        public static void Build(BundleBuilderData data, string outputPath, BuildTarget targetPlatform)
        {
            var assetBundleBuilds = GenerateAssetBundleBuilds(data);
            var zipArchiveBuilds = GenerateZipArchiveBuilds(data);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            AssetBundleManifest assetBundleManifest = null;
            ZipArchiveManifest zipArchiveManifest = null;
            if (assetBundleBuilds.Length != 0)
            {
                assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, assetBundleBuilds, BuildAssetBundleOptions.None, targetPlatform);
            }
            if (zipArchiveBuilds.Length != 0)
            {
                zipArchiveManifest = BuildZipArchives(outputPath, zipArchiveBuilds, targetPlatform);
            }
            // BuildPipeline.BuildPlayer()
            BuildManifest(data, outputPath, assetBundleManifest, zipArchiveManifest);
            Debug.Log($"build bundles finished {DateTime.Now}");
        }

        public static ZipArchiveManifest BuildZipArchives(string outputPath, BundleBuilderData.BundleInfo[] builds, BuildTarget targetPlatform)
        {
            var manifest = new ZipArchiveManifest();
            foreach (var bundle in builds)
            {
                var archiveEntry = new ZipArchiveEntry()
                {
                    name = bundle.name,
                };
                manifest.archives.Add(archiveEntry);
                var filename = Path.Combine(outputPath, bundle.name);
                using (var zip = new ZipOutputStream(File.OpenWrite(filename)))
                {
                    zip.IsStreamOwner = true;
                    foreach (var asset in bundle.assets)
                    {
                        BuildZipArchiveObject(zip, asset.target, archiveEntry);
                    }
                    zip.Close();
                }
            }
            return manifest;
        }

        private static void BuildZipArchiveObject(ZipOutputStream zip, Object asset, ZipArchiveEntry archiveEntry)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var fi = new FileInfo(assetPath);
            var name = ZipEntry.CleanName(assetPath);
            var entry = new ZipEntry(name);

            entry.DateTime = fi.LastWriteTimeUtc;
            entry.Size = fi.Length;
            // entry.Comment = "";
            zip.PutNextEntry(entry);
            using (var fs = fi.OpenRead())
            {
                var buf = new byte[1024 * 128];
                do
                {
                    var read = fs.Read(buf, 0, buf.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    zip.Write(buf, 0, read);
                } while (true);
            }
            zip.CloseEntry();
            archiveEntry.assets.Add(assetPath);
        }

        public static BundleBuilderData.BundleInfo[] GenerateZipArchiveBuilds(BundleBuilderData data)
        {
            var list = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type == BundleType.ZipArchive)
                {
                    list.Add(bundle);
                }
            }
            return list.ToArray();
        }

        // 获取指定包名的包对象信息
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

        // 生成清单
        public static void BuildManifest(BundleBuilderData data, string outputPath, AssetBundleManifest assetBundleManifest, ZipArchiveManifest zipArchiveManifest)
        {
            var manifest = new Manifest();
            if (assetBundleManifest != null)
            {
                var assetBundles = assetBundleManifest.GetAllAssetBundles();
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
            }
            if (zipArchiveManifest != null)
            {
                foreach (var zipArchive in zipArchiveManifest.archives)
                {
                    var bundleInfo = GetBundleInfo(data, zipArchive.name);
                    var zipArchivePath = Path.Combine(outputPath, zipArchive.name);
                    using (var stream = File.OpenRead(zipArchivePath))
                    {
                        var fileInfo = new FileInfo(zipArchivePath);
                        var checksum = new Utils.Crc16();
                        checksum.Update(stream);
                        var bundle = new Manifest.BundleInfo();
                        bundle.name = zipArchive.name;
                        bundle.checksum = checksum.hex;
                        bundle.size = (int)fileInfo.Length;
                        bundle.startup = bundleInfo.load == BundleLoad.Startup;
                        bundle.priority = bundleInfo.priority;
                        foreach (var assetPath in zipArchive.assets)
                        {
                            // var assetPath = AssetDatabase.GetAssetPath(asset.target);
                            bundle.assets.Add(assetPath);
                        }
                        // bundle.dependencies = null;
                        manifest.bundles.Add(bundle);
                    }
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

        // 是否包含指定名字的 bundle
        public static bool ContainsBundle(BundleBuilderData data, string bundleName)
        {
            foreach (var bundle in data.bundles)
            {
                if (bundle.name == bundleName)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ContainsAsset(BundleBuilderData data, Object assetObject)
        {
            foreach (var bundle in data.bundles)
            {
                foreach (var asset in bundle.assets)
                {
                    if (asset.target == assetObject)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // 包中是否存在指定的目标资源 (只比对target, 不检查实际资源列表)
        public static bool ContainsTarget(BundleBuilderData.BundleInfo bundleInfo, Object targetObject)
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

        // 将指定资源添加到 bundle 的 targets 列表中
        public static void Add(BundleBuilderData data, BundleBuilderData.BundleInfo bundleInfo, Object[] targetObjects)
        {
            foreach (var targetObject in targetObjects)
            {
                if (!ContainsTarget(bundleInfo, targetObject))
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
