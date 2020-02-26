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
        private static BundleBuilderData _data;

        public static BundleBuilderData GetData()
        {
            if (_data == null)
            {
                _data = BundleBuilderData.Load();
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
                    _data.MarkAsDirty();
                }
            }
            return _data;
        }

        // 根据 targets 遍历产生所有实际资源列表 assets
        public static bool Scan(BundleBuilderData data, BuildTarget targetPlatform)
        {
            data.Cleanup();
            foreach (var bundle in data.bundles)
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
            foreach (var target in bundle.targets)
            {
                if (target.enabled)
                {
                    Scan(data, bundle, target);
                }
            }
            bundle.Slice();
            return true;
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, BundleBuilderData.BundleAssetTarget target)
        {
            Scan(data, bundle, target, target.target);
        }

        public static void Scan(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, BundleBuilderData.BundleAssetTarget target, Object asset)
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
                    Scan(data, bundle, target, AssetDatabase.LoadMainAssetAtPath(directory));
                }
                foreach (var file in Directory.GetFiles(targetPath))
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }
                    var fileAsset = AssetDatabase.LoadMainAssetAtPath(file);
                    Scan(data, bundle, target, fileAsset);
                }
            }
            else
            {
                if (CollectAsset(data, bundle, asset))
                {
                    if (bundle.AddAssetOrder(asset))
                    {
                        data.MarkAsDirty();
                    }
                }
            }
        }

        public static bool CollectAsset(BundleBuilderData data, BundleBuilderData.BundleInfo bundle, Object asset)
        {
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
                            if (IsRuleMatched(rule, asset))
                            {
                                break;
                            }
                        }
                        else
                        {
                            if (IsRuleMatched(rule, asset))
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
                    if (!ContainsAsset(data, asset))
                    {
                        split.assets.Add(asset);
                    }
                    return true;
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
                else if ((rule.assetTypes & BundleAssetTypes.Animation) == 0 && (asset is Animation || asset is AnimationClip))
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

        public static bool IsRuleMatched(BundleBuilderData.BundleSplitRule rule, Object asset)
        {
            if (rule.type == BundleBuilderData.BundleSplitType.Prefix)
            {
                if (!asset.name.StartsWith(rule.keyword))
                {
                    return false;
                }
            }
            else if (rule.type == BundleBuilderData.BundleSplitType.Suffix)
            {
                if (!asset.name.EndsWith(rule.keyword))
                {
                    return false;
                }
            }
            return IsAssetTypeMatched(rule, asset);
        }

        public static AssetBundleBuild[] GenerateAssetBundleBuilds(BundleBuilderData data)
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type != Manifest.BundleType.AssetBundle)
                {
                    continue;
                }
                for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
                {
                    var bundleSplit = bundle.splits[splitIndex];
                    for (var sliceIndex = 0; sliceIndex < bundleSplit.slices.Count; sliceIndex++)
                    {
                        var bundleSlice = bundleSplit.slices[sliceIndex];
                        var assetNames = new List<string>();
                        for (var assetIndex = 0; assetIndex < bundleSlice.assets.Count; assetIndex++)
                        {
                            var asset = bundleSlice.assets[assetIndex];
                            var assetPath = AssetDatabase.GetAssetPath(asset);
                            assetNames.Add(assetPath);
                        }
                        if (assetNames.Count != 0)
                        {
                            var names = assetNames.ToArray();
                            var build = new AssetBundleBuild();
                            build.assetBundleName = bundleSlice.name;
                            build.assetNames = names;
                            build.addressableNames = names;
                            builds.Add(build);
                            // Debug.Log($"{build.assetBundleName}: {build.assetNames.Length}");
                        }
                        else
                        {
                            Debug.LogWarning($"empty build split {bundle.name}_{splitIndex}");
                        }
                    }
                }
            }
            return builds.ToArray();
        }

        // 生成打包 
        public static void Build(BundleBuilderData data, string outputPath, BuildTarget targetPlatform)
        {
            BundleBuilder.Scan(data, targetPlatform);
            var assetBundleBuilds = GenerateAssetBundleBuilds(data);
            var zipArchiveBuilds = GenerateZipArchiveBuilds(data);
            var fileListBuilds = GenerateFileListBuilds(data);
            // var sceneBundleBuilds = GenerateSceneBundleBuilds(data);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            AssetBundleManifest assetBundleManifest = null;
            ZipArchiveBuild zipArchiveBuild = null;
            FileListBuild fileListBuild = null;
            // UnityEditor.Build.Reporting.BuildReport report = null;
            // if (sceneBundleBuilds.Length != 0)
            // {
            //     Debug.Log($"build {sceneBundleBuilds.Length} scene bundles");
            //     var levels = new List<string>(sceneBundleBuilds.Length);
            //     foreach (var build in sceneBundleBuilds)
            //     {
            //         levels.Add(build.scenePath);
            //     }
            //     report = BuildPipeline.BuildPlayer(levels.ToArray(), outputPath, targetPlatform, BuildOptions.BuildAdditionalStreamedScenes);
            // }
            if (assetBundleBuilds.Length != 0)
            {
                assetBundleManifest = BuildPipeline.BuildAssetBundles(outputPath, assetBundleBuilds, BuildAssetBundleOptions.None, targetPlatform);
            }
            if (zipArchiveBuilds.Length != 0)
            {
                zipArchiveBuild = BuildZipArchives(outputPath, zipArchiveBuilds, targetPlatform);
            }
            if (fileListBuilds.Length != 0)
            {
                fileListBuild = BuildFileLists(outputPath, fileListBuilds, targetPlatform);
            }
            EmbeddedManifest embeddedManifest;
            BuildManifest(data, outputPath, assetBundleManifest, zipArchiveBuild, fileListBuild, out embeddedManifest);
            PrepareStreamingAssets(data, outputPath, embeddedManifest);
            Cleanup(outputPath, assetBundleManifest, zipArchiveBuild, fileListBuild, embeddedManifest);
            Debug.Log($"build bundles finished {DateTime.Now}. {assetBundleBuilds.Length} assetbundles. {zipArchiveBuilds.Length} zip archives. {fileListBuilds.Length} file lists. {embeddedManifest.bundles.Count} bundles to streamingassets.");
        }

        private static void PrepareStreamingAssets(BundleBuilderData data, string outputPath, EmbeddedManifest embeddedManifest)
        {
            if (embeddedManifest.bundles.Count > 0)
            {
                if (!Directory.Exists(EmbeddedManifest.BundlesPath))
                {
                    Directory.CreateDirectory(EmbeddedManifest.BundlesPath);
                }
                File.Copy(Path.Combine(outputPath, EmbeddedManifest.FileName), Path.Combine(EmbeddedManifest.BundlesPath, EmbeddedManifest.FileName), true);
                foreach (var bundleInfo in embeddedManifest.bundles)
                {
                    File.Copy(Path.Combine(outputPath, bundleInfo.name), Path.Combine(EmbeddedManifest.BundlesPath, bundleInfo.name), true);
                }
                AssetDatabase.Refresh();
                // cleanup
                foreach (var file in Directory.GetFiles(EmbeddedManifest.BundlesPath))
                {
                    var fi = new FileInfo(file);
                    var match = false;
                    if (fi.Name == EmbeddedManifest.FileName || fi.Name == EmbeddedManifest.FileName + ".meta")
                    {
                        continue;
                    }
                    foreach (var bundleInfo in embeddedManifest.bundles)
                    {
                        if (fi.Name == bundleInfo.name || fi.Name == bundleInfo.name + ".meta")
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match)
                    {
                        fi.Delete();
                    }
                }
                AssetDatabase.Refresh();
            }
            else
            {
                if (Directory.Exists(EmbeddedManifest.BundlesPath))
                {
                    Directory.Delete(EmbeddedManifest.BundlesPath);
                }
            }
        }

        private static void Cleanup(string outputPath,
                                    AssetBundleManifest assetBundleManifest,
                                    ZipArchiveBuild zipArchiveManifest,
                                    FileListBuild fileListBuild,
                                    EmbeddedManifest embeddedManifest)
        {
            foreach (var file in Directory.GetFiles(outputPath))
            {
                var fi = new FileInfo(file);
                var match = false;
                if (
                    fi.Name == "AssetBundles" ||
                    fi.Name == "AssetBundles.manifest" ||
                    fi.Name == "checksum.txt" ||
                    fi.Name == "manifest.json"
                )
                {
                    match = true;
                }
                if (fi.Name == EmbeddedManifest.FileName)
                {
                    if (embeddedManifest.bundles.Count > 0)
                    {
                        match = true;
                    }
                }
                if (!match && assetBundleManifest != null)
                {
                    foreach (var assetBundle in assetBundleManifest.GetAllAssetBundles())
                    {
                        if (fi.Name == assetBundle || fi.Name == assetBundle + ".manifest")
                        {
                            match = true;
                            break;
                        }
                    }
                }
                if (!match && zipArchiveManifest != null)
                {
                    foreach (var zipArchive in zipArchiveManifest.archives)
                    {
                        if (fi.Name == zipArchive.name)
                        {
                            match = true;
                            break;
                        }
                    }
                }
                if (!match && fileListBuild != null)
                {
                    foreach (var fileList in fileListBuild.fileLists)
                    {
                        if (fi.Name == fileList.name)
                        {
                            match = true;
                            break;
                        }
                    }
                }
                if (!match)
                {
                    // Debug.LogWarning("delete unused file: " + fi.Name);
                    try
                    {
                        fi.Delete();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(exception);
                    }
                }
            }
        }

        public static FileListBuild BuildFileLists(string outputPath, BundleBuilderData.BundleInfo[] builds, BuildTarget targetPlatform)
        {
            var build = new FileListBuild();
            foreach (var bundle in builds)
            {
                var entry = new FileListBuildEntry()
                {
                    name = bundle.name,
                };
                build.fileLists.Add(entry);
                var filename = Path.Combine(outputPath, bundle.name);
                var manifest = new FileListManifest();
                foreach (var split in bundle.splits)
                {
                    foreach (var slice in split.slices)
                    {
                        foreach (var asset in slice.assets)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(asset);
                            var fileEntry = GenFileEntry(assetPath, assetPath);
                            manifest.files.Add(fileEntry);
                            // Debug.LogFormat("gen {0}", fileEntry.name);
                        }
                    }
                }
                using (var sw = new StreamWriter(File.Open(filename, FileMode.Truncate, FileAccess.Write, FileShare.Write)))
                {
                    var jsonString = JsonUtility.ToJson(manifest);
                    sw.Write(jsonString);
                }
            }
            return build;
        }

        public static ZipArchiveBuild BuildZipArchives(string outputPath, BundleBuilderData.BundleInfo[] builds, BuildTarget targetPlatform)
        {
            var build = new ZipArchiveBuild();
            foreach (var bundle in builds)
            {
                var entry = new ZipArchiveBuildEntry()
                {
                    name = bundle.name,
                };
                build.archives.Add(entry);
                var filename = Path.Combine(outputPath, bundle.name);
                using (var zip = new ZipOutputStream(File.Open(filename, FileMode.Truncate, FileAccess.Write, FileShare.Write)))
                {
                    zip.IsStreamOwner = true;
                    foreach (var split in bundle.splits)
                    {
                        foreach (var slice in split.slices)
                        {
                            foreach (var asset in slice.assets)
                            {
                                BuildZipArchiveObject(zip, asset, entry);
                            }
                        }
                    }
                }
            }
            return build;
        }

        private static void BuildZipArchiveObject(ZipOutputStream zip, Object asset, ZipArchiveBuildEntry archiveEntry)
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

        // public static SceneBundleBuild[] GenerateSceneBundleBuilds(BundleBuilderData data)
        // {
        //     var list = new List<SceneBundleBuild>();
        //     foreach (var bundle in data.bundles)
        //     {
        //         if (bundle.type == BundleType.SceneBundle)
        //         {
        //             var build = new SceneBundleBuild();
        //             build.name = bundle.name;
        //             foreach (var asset in bundle.assets)
        //             {
        //                 if (asset.target is SceneAsset)
        //                 {
        //                     build.scenePath = AssetDatabase.GetAssetPath(asset.target);
        //                 }
        //             }
        //             list.Add(build);
        //         }
        //     }
        //     return list.ToArray();
        // }

        public static BundleBuilderData.BundleInfo[] GenerateFileListBuilds(BundleBuilderData data)
        {
            var list = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type != Manifest.BundleType.FileList)
                {
                    continue;
                }
                list.Add(bundle);
                // for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
                // {
                //     var bundleSplit = bundle.splits[splitIndex];
                //     for (var sliceIndex = 0; sliceIndex < bundleSplit.slices.Count; sliceIndex++)
                //     {
                //         var bundleSlice = bundleSplit.slices[sliceIndex];
                //         var assetNames = new List<string>();
                //         // for (var assetIndex = 0; assetIndex < bundleSlice.assets.Count; assetIndex++)
                //         // {
                //         //     var asset = bundleSlice.assets[assetIndex];
                //         //     var assetPath = AssetDatabase.GetAssetPath(asset);
                //         //     assetNames.Add(assetPath);
                //         // }
                //         // if (assetNames.Count != 0)
                //         // {
                //         //     var names = assetNames.ToArray();
                //         //     var build = new AssetBundleBuild();
                //         //     build.assetBundleName = bundleSlice.name;
                //         //     build.assetNames = names;
                //         //     build.addressableNames = names;
                //         //     builds.Add(build);
                //         //     // Debug.Log($"{build.assetBundleName}: {build.assetNames.Length}");
                //         // }
                //         // else
                //         // {
                //         //     Debug.LogWarning($"empty build split {bundle.name}_{splitIndex}");
                //         // }
                //     }
                // }
            }
            return list.ToArray();
        }

        public static BundleBuilderData.BundleInfo[] GenerateZipArchiveBuilds(BundleBuilderData data)
        {
            var list = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type == Manifest.BundleType.ZipArchive)
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

        // 获取指定包名的包对象信息
        public static bool TryGetBundleSlice(BundleBuilderData data, string bundleName, out BundleBuilderData.BundleInfo bundleInfo, out BundleBuilderData.BundleSlice bundleSlice)
        {
            foreach (var bundle in data.bundles)
            {
                foreach (var split in bundle.splits)
                {
                    foreach (var slice in split.slices)
                    {
                        if (slice.name == bundleName)
                        {
                            bundleInfo = bundle;
                            bundleSlice = slice;
                            return true;
                        }
                    }
                }
            }
            bundleInfo = null;
            bundleSlice = null;
            return false;
        }

        public static FileEntry GenFileEntry(string entryName, string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                var checksum = new Utils.Crc16();
                checksum.Update(stream);
                return new FileEntry()
                {
                    name = entryName,
                    checksum = checksum.hex,
                    size = (int)fileInfo.Length,
                };
            }
        }

        // 生成清单
        public static void BuildManifest(BundleBuilderData data, string outputPath,
            AssetBundleManifest assetBundleManifest,
            ZipArchiveBuild zipArchiveManifest,
            FileListBuild fileListManifest,
            out EmbeddedManifest embeddedManifest)
        {
            var manifest = new Manifest();
            embeddedManifest = new EmbeddedManifest();
            if (assetBundleManifest != null)
            {
                var assetBundles = assetBundleManifest.GetAllAssetBundles();
                foreach (var assetBundle in assetBundles)
                {
                    BundleBuilderData.BundleInfo bundleInfo;
                    BundleBuilderData.BundleSlice bundleSlice;
                    if (TryGetBundleSlice(data, assetBundle, out bundleInfo, out bundleSlice))
                    {
                        // Debug.Log(bundleInfo.name);
                        var assetBundlePath = Path.Combine(outputPath, assetBundle);
                        var fileEntry = GenFileEntry(bundleSlice.name, assetBundlePath);
                        var bundle = new Manifest.BundleInfo();

                        bundle.type = Manifest.BundleType.AssetBundle;
                        bundle.name = fileEntry.name;
                        bundle.checksum = fileEntry.checksum;
                        bundle.size = fileEntry.size;
                        bundle.load = bundleInfo.load;
                        bundle.priority = bundleInfo.priority;
                        foreach (var asset in bundleSlice.assets)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(asset);
                            bundle.assets.Add(assetPath);
                        }
                        bundle.dependencies = assetBundleManifest.GetAllDependencies(assetBundle);
                        manifest.bundles.Add(bundle);
                        if (bundleInfo.streamingAssets)
                        {
                            embeddedManifest.bundles.Add(fileEntry);
                        }
                    }
                }
            }
            if (zipArchiveManifest != null)
            {
                foreach (var zipArchive in zipArchiveManifest.archives)
                {
                    var bundleInfo = GetBundleInfo(data, zipArchive.name);
                    var zipArchivePath = Path.Combine(outputPath, zipArchive.name);
                    var fileEntry = GenFileEntry(zipArchive.name, zipArchivePath);
                    var bundle = new Manifest.BundleInfo();

                    bundle.type = Manifest.BundleType.ZipArchive;
                    bundle.name = zipArchive.name;
                    bundle.checksum = fileEntry.checksum;
                    bundle.size = fileEntry.size;
                    bundle.load = bundleInfo.load;
                    bundle.priority = bundleInfo.priority;
                    foreach (var assetPath in zipArchive.assets)
                    {
                        // var assetPath = AssetDatabase.GetAssetPath(asset.target);
                        bundle.assets.Add(assetPath);
                    }
                    // bundle.dependencies = null;
                    manifest.bundles.Add(bundle);
                    if (bundleInfo.streamingAssets)
                    {
                        embeddedManifest.bundles.Add(fileEntry);
                    }
                }
            }
            if (fileListManifest != null)
            {
                foreach (var fileList in fileListManifest.fileLists)
                {
                    var bundleInfo = GetBundleInfo(data, fileList.name);
                    var fileListPath = Path.Combine(outputPath, fileList.name);
                    var fileEntry = GenFileEntry(fileList.name, fileListPath);
                    var bundle = new Manifest.BundleInfo();

                    bundle.type = Manifest.BundleType.FileList;
                    bundle.name = fileList.name;
                    bundle.checksum = fileEntry.checksum;
                    bundle.size = fileEntry.size;
                    bundle.load = bundleInfo.load;
                    bundle.priority = bundleInfo.priority;

                    foreach (var bundleTargets in bundleInfo.targets)
                    {
                        var target = bundleTargets.target;
                        var targetPath = AssetDatabase.GetAssetPath(target);
                        bundle.assets.Add(targetPath);
                    }
                    manifest.bundles.Add(bundle);
                    if (bundleInfo.streamingAssets)
                    {
                        embeddedManifest.bundles.Add(fileEntry);
                    }
                }
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            OutputManifest(manifest, outputPath);
            OutputEmbeddedManifest(embeddedManifest, outputPath);
        }

        // write manifest & checksum of manifest 
        private static void OutputManifest(Manifest manifest, string outputPath)
        {
            var json = JsonUtility.ToJson(manifest);
            var jsonChecksum = Utils.Crc16.ToString(Utils.Crc16.ComputeChecksum(System.Text.Encoding.UTF8.GetBytes(json)));
            var manifestPath = Path.Combine(outputPath, "manifest.json");
            var manifestChecksumPath = Path.Combine(outputPath, "checksum.txt");
            File.WriteAllText(manifestPath, json);
            File.WriteAllText(manifestChecksumPath, jsonChecksum);
        }

        // write embedded manifest to streamingassets 
        private static void OutputEmbeddedManifest(EmbeddedManifest embeddedManifest, string outputPath)
        {
            if (embeddedManifest.bundles.Count > 0)
            {
                var json = JsonUtility.ToJson(embeddedManifest);
                var manifestPath = Path.Combine(outputPath, "streamingassets-manifest.json");
                File.WriteAllText(manifestPath, json);
            }
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
                foreach (var split in bundle.splits)
                {
                    if (split.assets.Contains(assetObject))
                    {
                        return true;
                    }
                    foreach (var slice in split.slices)
                    {
                        foreach (var asset in slice.assets)
                        {
                            if (asset == assetObject)
                            {
                                return true;
                            }
                        }
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
            data.MarkAsDirty();
        }
    }
}
