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
using ICSharpCode.SharpZipLib.Zip.Compression;
using NUnit.Framework;
using UnityEditor.WindowsStandalone;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public partial class BundleBuilder
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

        public static void BuildPackages(BundleBuilderData data, string outputPath, PackagePlatform platform)
        {
            BuildSinglePlatformPackages(new PackageSharedBuildInfo() { data = data, outputPath = outputPath }, platform);
        }

        public static BuildTarget ToBuildTarget(PackagePlatform platform)
        {
            switch (platform)
            {
                case PackagePlatform.Any: return EditorUserBuildSettings.activeBuildTarget;
                case PackagePlatform.Android: return BuildTarget.Android;
                case PackagePlatform.IOS: return BuildTarget.iOS;
                case PackagePlatform.Windows64: return BuildTarget.StandaloneWindows64;
                case PackagePlatform.MacOS: return BuildTarget.StandaloneOSX;
            }

            throw new NotSupportedException();
        }

        public static void BuildSinglePlatformPackages(PackageSharedBuildInfo sharedBuildInfo, PackagePlatform platform)
        {
            _BuildPackages(new PackageBuildInfo(sharedBuildInfo, platform, ToBuildTarget(platform)));
        }

        // 生成打包 
        private static void _BuildPackages(PackageBuildInfo packageBuildInfo)
        {
            Debug.Log($"building bundles...");
            Scan(packageBuildInfo.data);

            var assetBundleBuilds = GenerateAssetBundleBuilds(packageBuildInfo);
            var zipArchiveBuilds = GenerateZipArchiveBuilds(packageBuildInfo);
            var fileListBuilds = GenerateFileListBuilds(packageBuildInfo);
            var rawFileBuilds = GenerateRawFileBuilds(packageBuildInfo);

            AssetBundleManifest assetBundleManifest = null;
            ZipArchiveManifest zipArchiveManifest = null;
            FileListManifest fileListManifest = null;
            RawFileManifest rawFileManifest = null;
            if (assetBundleBuilds.Length != 0)
            {
                assetBundleManifest = BuildAssetBundles(packageBuildInfo, assetBundleBuilds);
            }

            if (zipArchiveBuilds.Count != 0)
            {
                zipArchiveManifest = BuildZipArchives(packageBuildInfo, zipArchiveBuilds);
            }

            if (fileListBuilds.Length != 0)
            {
                fileListManifest = BuildFileLists(packageBuildInfo, fileListBuilds);
            }

            if (rawFileBuilds.Length != 0)
            {
                rawFileManifest = BuildRawFiles(packageBuildInfo, rawFileBuilds);
            }

            var embeddedManifest = BuildFinalPackages(packageBuildInfo, assetBundleManifest, zipArchiveManifest, fileListManifest, rawFileManifest);
            Cleanup(packageBuildInfo, assetBundleManifest, zipArchiveManifest, fileListManifest, rawFileManifest, embeddedManifest);
            packageBuildInfo.DoAnalyze();
            packageBuildInfo.data.build++;
            packageBuildInfo.data.MarkAsDirty();
            Debug.Log(
                $"{packageBuildInfo.packagePath}: build bundles finished. {assetBundleBuilds.Length} assetbundles. {zipArchiveBuilds.Count} zip archives. {fileListBuilds.Length} file lists. {embeddedManifest.bundles.Count} bundles to streamingassets.");
        }

        // 将首包资源复制到 StreamingAssets 目录 (在 BuildPlayer 之前调用)
        public static void BuildStreamingAssets(PackageBuildInfo buildInfo, FileListManifest fileListManifest)
        {
            var packagePath = buildInfo.packagePath;
            var embeddedManifest = ReadEmbeddedManifest(packagePath);
            if (embeddedManifest != null && embeddedManifest.bundles.Count > 0)
            {
                File.Copy(Path.Combine(packagePath, Manifest.EmbeddedManifestFileName),
                    Path.Combine(buildInfo.streamingAssetsPath, Manifest.EmbeddedManifestFileName), true);
                foreach (var bundleInfo in embeddedManifest.bundles)
                {
                    // Debug.LogFormat("copy {0}", bundleInfo.name);
                    File.Copy(Path.Combine(packagePath, bundleInfo.name),
                        Path.Combine(buildInfo.streamingAssetsPath, bundleInfo.name), true);

                    //TODO: streamingassets copy
                    if (fileListManifest != null) 
                    {
                        foreach (var entry in fileListManifest.fileEntrys)
                        {
                            if (entry.streamingAssets && CopyRawFile(buildInfo.streamingAssetsPath, entry.assetPath))
                            {
                                Debug.LogWarningFormat("copy xxx {0}", entry.assetPath);
                            }
                        }
                    }
                }

                AssetDatabase.Refresh();
                // cleanup
                foreach (var dir in Directory.GetDirectories(buildInfo.streamingAssetsPath))
                {
                    CleanupRecursively(dir, "Assets", fileListManifest, true);
                }

                foreach (var file in Directory.GetFiles(buildInfo.streamingAssetsPath))
                {
                    var fi = new FileInfo(file);
                    var match = false;
                    if (fi.Name == Manifest.EmbeddedManifestFileName ||
                        fi.Name == Manifest.EmbeddedManifestFileName + ".meta")
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
                PathUtils.CleanupDirectoryRecursively(buildInfo.streamingAssetsPath);
            }
        }

        private static HashSet<string> builtinFiles = new HashSet<string>(new string[]
        {
//            "AssetBundles",
//            "AssetBundles.manifest",
            Manifest.ChecksumFileName,
            Manifest.ManifestFileName,
        });

        private static string NormalizeFileName(string filename)
        {
            return filename.Replace('\\', '/');
        }

        private static void CleanupRecursively(string innerPath, string relativeDir, FileListManifest fileListManifest,
            bool bStreamingAssets)
        {
            foreach (var dir in Directory.GetDirectories(innerPath))
            {
                var info = new DirectoryInfo(dir);
                CleanupRecursively(dir, relativeDir + '/' + info.Name, fileListManifest, bStreamingAssets);
            }

            foreach (var file in Directory.GetFiles(innerPath))
            {
                var fi = new FileInfo(file);
                var filename = NormalizeFileName(relativeDir + '/' + fi.Name);
                var match = false;

                if (fileListManifest != null)
                {
                    foreach (var entry in fileListManifest.fileEntrys)
                    {
                        // Debug.LogFormat("!! {0} {1}", file, filename);
                        if (filename == entry.assetPath)
                        {
                            if (!bStreamingAssets || entry.streamingAssets)
                            {
                                match = true;
                                break;
                            }
                        }
                    }
                }

                if (!match)
                {
                    fi.Delete();
                }
            }
        }

        private static void Cleanup(PackageBuildInfo buildInfo,
            AssetBundleManifest assetBundleManifest,
            ZipArchiveManifest zipArchiveManifest,
            FileListManifest fileListManifest,
            RawFileManifest rawFileManifest,
            EmbeddedManifest embeddedManifest)
        {
            foreach (var dir in Directory.GetDirectories(buildInfo.packagePath))
            {
                CleanupRecursively(dir, "Assets", fileListManifest, false);
            }

            foreach (var file in Directory.GetFiles(buildInfo.packagePath))
            {
                var match = false;
                var fi = new FileInfo(file);
                var filename = fi.Name;

                if (builtinFiles.Contains(filename))
                {
                    match = true;
                }

                if (!match && buildInfo.filelist.Contains(filename))
                {
                    match = true;
                }

                if (!match && filename == Manifest.EmbeddedManifestFileName && embeddedManifest.bundles.Count > 0)
                {
                    match = true;
                }

                if (!match)
                {
                    Debug.LogWarning("delete unused file: " + filename);
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

        public static RawFileManifest BuildRawFiles(PackageBuildInfo buildInfo, RawFileBuild[] builds)
        {
            var manifest = new RawFileManifest();
            // foreach (var build in builds)
            // {
            //     var entry = new RawFileManifestEntry()
            //     {
            //         name = build.name,
            //     };
            //     manifest.xxx.Add(entry);
            //     
            // }
            return manifest;
        }

        public static FileListManifest BuildFileLists(PackageBuildInfo buildInfo, BundleBuilderData.BundleInfo[] builds)
        {
            var build = new FileListManifest();
            foreach (var bundle in builds)
            {
                var entry = new FileListManifestEntry()
                {
                    name = bundle.name,
                };
                build.fileLists.Add(entry);
                var filename = Path.Combine(buildInfo.packagePath, bundle.name);
                var manifest = new UnityFS.FileListManifest();
                foreach (var split in bundle.splits)
                {
                    foreach (var slice in split.slices)
                    {
                        foreach (var assetGuid in slice.assetGuids)
                        {
                            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            var fileEntry = GenFileEntry(assetPath, assetPath);
                            manifest.files.Add(fileEntry);
                            if (CopyRawFile(buildInfo.packagePath, assetPath))
                            {
                                var fileListManifestFileInfo = new FileListManifestFileInfo()
                                {
                                    assetPath = assetPath,
                                    streamingAssets = bundle.streamingAssets
                                };
                                build.fileEntrys.Add(fileListManifestFileInfo);
                            }

                            // FileUtil.CopyFileOrDirectory(assetPath, outPath);
                            // Debug.LogFormat("gen {0} from {1}", outFilePath, assetPath);
                        }
                    }
                }

                var jsonString = JsonUtility.ToJson(manifest);
                File.WriteAllText(filename, jsonString);
            }

            return build;
        }

        private static bool CopyRawFile(string outputPath, string assetPath)
        {
            var outFilePath = Path.Combine(outputPath, assetPath).Replace('\\', '/');
            var dir = Path.GetDirectoryName(outFilePath);
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(assetPath, outFilePath, true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogErrorFormat("copy file list file failed: {0}\n{1}", outFilePath, exception);
            }

            return false;
        }

        public static AssetBundleManifest BuildAssetBundles(PackageBuildInfo buildInfo,
            AssetBundleBuild[] assetBundleBuilds)
        {
            var options = BuildAssetBundleOptions.None;
            if (buildInfo.data.disableTypeTree)
            {
                options |= BuildAssetBundleOptions.DisableWriteTypeTree;
            }
            if (buildInfo.data.lz4Compression)
            {
                options |= BuildAssetBundleOptions.ChunkBasedCompression;
            }
            return BuildPipeline.BuildAssetBundles(buildInfo.assetBundlePath, assetBundleBuilds,
                options,
                buildInfo.buildTarget);
        }

        //TODO: zip 打包拆包
        public static ZipArchiveManifest BuildZipArchives(PackageBuildInfo buildInfo, List<ZipArchiveBuild> builds)
        {
            var manifest = new ZipArchiveManifest();
            foreach (var build in builds)
            {
                var entry = new ZipArchiveManifestEntry()
                {
                    name = build.name,
                };
                manifest.archives.Add(entry);
                var zipArchiveFileName = Path.Combine(buildInfo.zipArchivePath, entry.name);
                if (File.Exists(zipArchiveFileName))
                {
                    File.Delete(zipArchiveFileName);
                }

                using (var zip = new ZipOutputStream(File.Open(zipArchiveFileName, FileMode.Create, FileAccess.Write,
                    FileShare.Write)))
                {
                    zip.IsStreamOwner = true;
                    foreach (var assetPath in build.assetPaths)
                    {
                        BuildZipArchiveObject(zip, assetPath, entry);
                    }
                }
            }

            return manifest;
        }

        private static void BuildZipArchiveObject(ZipOutputStream zip, string assetPath,
            ZipArchiveManifestEntry archiveEntry)
        {
            var fi = new FileInfo(assetPath);
            var name = ZipEntry.CleanName(assetPath);
            var entry = new ZipEntry(name) { DateTime = fi.LastWriteTimeUtc, Size = fi.Length };

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

        public static RawFileBuild[] GenerateRawFileBuilds(PackageBuildInfo buildInfo)
        {
            var data = buildInfo.data;
            var list = new List<RawFileBuild>();

            foreach (var bundle in data.bundles)
            {
                if (bundle.type != Manifest.BundleType.RawFile)
                {
                    continue;
                }

                for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
                {
                    var bundleSplit = bundle.splits[splitIndex];
                    for (var sliceIndex = 0; sliceIndex < bundleSplit.slices.Count; sliceIndex++)
                    {
                        var bundleSlice = bundleSplit.slices[sliceIndex];
                        if (bundleSlice.IsBuild(buildInfo.buildPlatform))
                        {
                            var assetNames = new List<string>();
                            for (var assetIndex = 0; assetIndex < bundleSlice.assetGuids.Count; assetIndex++)
                            {
                                var assetGuid = bundleSlice.assetGuids[assetIndex];
                                if (data.IsPackAsset(assetGuid))
                                {
                                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    assetNames.Add(assetPath);
                                }
                            }

                            if (assetNames.Count != 0)
                            {
                                var names = assetNames.ToArray();
                                var build = new RawFileBuild();
                                build.name = bundleSlice.name;
                                build.assetNames = names;
                                list.Add(build);
                                // Debug.Log($"{build.assetBundleName}: {build.assetNames.Length}");
                            }
                        }
                    }
                }
            }

            return list.ToArray();
        }

        public static BundleBuilderData.BundleInfo[] GenerateFileListBuilds(PackageBuildInfo buildInfo)
        {
            var data = buildInfo.data;
            var list = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type != Manifest.BundleType.FileList)
                {
                    continue;
                }

                list.Add(bundle);
            }

            return list.ToArray();
        }

        public static List<ZipArchiveBuild> GenerateZipArchiveBuilds(PackageBuildInfo buildInfo)
        {
            var data = buildInfo.data;
            var builds = new List<ZipArchiveBuild>();
            foreach (var bundle in data.bundles)
            {
                if (bundle.type != Manifest.BundleType.ZipArchive)
                {
                    continue;
                }

                for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
                {
                    var bundleSplit = bundle.splits[splitIndex];
                    for (var sliceIndex = 0; sliceIndex < bundleSplit.slices.Count; sliceIndex++)
                    {
                        var bundleSlice = bundleSplit.slices[sliceIndex];
                        if (bundleSlice.IsBuild(buildInfo.buildPlatform))
                        {
                            var assetNames = new List<string>();
                            for (var assetIndex = 0; assetIndex < bundleSlice.assetGuids.Count; assetIndex++)
                            {
                                var assetGuid = bundleSlice.assetGuids[assetIndex];
                                if (data.IsPackAsset(assetGuid))
                                {
                                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    assetNames.Add(assetPath);
                                }
                            }

                            if (assetNames.Count != 0)
                            {
                                var build = new ZipArchiveBuild();
                                build.name = bundleSlice.name;
                                build.assetPaths = assetNames;
                                builds.Add(build);
                                // Debug.Log($"{build.assetBundleName}: {build.assetNames.Length}");
                            }
                            else
                            {
                                Debug.Log($"skip empty bundle slice {bundleSlice.name}");
                            }
                        }
                    }
                }
            }

            return builds;
        }

        public static AssetBundleBuild[] GenerateAssetBundleBuilds(PackageBuildInfo buildInfo)
        {
            var data = buildInfo.data;
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
                        if (bundleSlice.IsBuild(buildInfo.buildPlatform))
                        {
                            var assetNames = new List<string>();
                            for (var assetIndex = 0; assetIndex < bundleSlice.assetGuids.Count; assetIndex++)
                            {
                                var assetGuid = bundleSlice.assetGuids[assetIndex];
                                if (data.IsPackAsset(assetGuid))
                                {
                                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                                    assetNames.Add(assetPath);
                                }
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
                        }
                    }
                }
            }

            return builds.ToArray();
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
        public static bool TryGetBundleSlice(BundleBuilderData data, string bundleName,
            out BundleBuilderData.BundleInfo bundleInfo,
            out BundleBuilderData.BundleSplit bundleSplit,
            out BundleBuilderData.BundleSlice bundleSlice)
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
                            bundleSplit = split;
                            bundleSlice = slice;
                            return true;
                        }
                    }
                }
            }

            bundleInfo = null;
            bundleSplit = null;
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

        private static FileEntry EncryptFile(BundleBuilderData data, PackageBuildInfo buildInfo, string sourcePath,
            string name)
        {
            var rawFilePath = Path.Combine(sourcePath, name);
            var bytes = File.ReadAllBytes(rawFilePath);
            return EncryptData(buildInfo, name, bytes);
        }


        private static FileEntry EncryptData(PackageBuildInfo buildInfo, string name,
            byte[] bytes)
        {
            var encFilePath = Path.Combine(buildInfo.packagePath, name);
            var password = buildInfo.data.encryptionKey + name;
            var key = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
            var iv = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(password + Manifest.EncryptionSalt));
            buildInfo.filelist.Add(name);
            if (File.Exists(encFilePath))
            {
                File.Delete(encFilePath);
            }

            using (var fout = File.Open(encFilePath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (var algo = Rijndael.Create())
                {
                    algo.Padding = PaddingMode.Zeros;
                    var cryptor = algo.CreateEncryptor(key, iv);
                    using (var cryptStream = new CryptoStream(fout, cryptor, CryptoStreamMode.Write))
                    {
                        cryptStream.Write(bytes, 0, bytes.Length);
                        cryptStream.FlushFinalBlock();
                    }
                }
            }

            var fileEntry = GenFileEntry(name, encFilePath);
            fileEntry.rsize = bytes.Length;
            return fileEntry;
        }

        public static FileEntry EncryptFileEntry(BundleBuilderData data, PackageBuildInfo buildInfo, bool encrypted,
            string name, string sourcePath)
        {
            if (encrypted)
            {
                return EncryptFile(data, buildInfo, sourcePath, name);
            }

            var targetFilePath = Path.Combine(buildInfo.packagePath, name);
            if (sourcePath != buildInfo.packagePath)
            {
                var rawFilePath = Path.Combine(sourcePath, name);
                File.Copy(rawFilePath, targetFilePath, true);
            }

            buildInfo.filelist.Add(name);
            return GenFileEntry(name, targetFilePath);
        }

        // 计算指定 slice 中最高的资源优先级作为包优先级 (最低不低于 bundleInfo定义的优先级)
        private static int GetPriority(PackageBuildInfo buildInfo, BundleBuilderData.BundleInfo bundleInfo,
            BundleBuilderData.BundleSlice bundleSlice)
        {
            var priority = bundleInfo.priority;
            for (int i = 0, size = bundleSlice.assetGuids.Count; i < size; i++)
            {
                var guid = bundleSlice.assetGuids[i];
                var attrs = buildInfo.data.GetAssetAttributes(guid);
                if (attrs != null)
                {
                    if (attrs.priority > priority)
                    {
                        priority = attrs.priority;
                    }
                }
            }
            return priority;
        }

        // 生成最终包文件, 生成最终清单
        public static EmbeddedManifest BuildFinalPackages(PackageBuildInfo buildInfo,
            AssetBundleManifest assetBundleManifest,
            ZipArchiveManifest zipArchiveManifest,
            FileListManifest fileListManifest,
            RawFileManifest rawFileManifest)
        {
            var data = buildInfo.data;
            var manifest = new Manifest();
            manifest.build = buildInfo.data.build;
            manifest.timestamp = (int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            manifest.tag = buildInfo.sharedBuildInfo.tag;
            var embeddedManifest = new EmbeddedManifest();
            if (assetBundleManifest != null)
            {
                var assetBundles = assetBundleManifest.GetAllAssetBundles();
                foreach (var assetBundle in assetBundles)
                {
                    BundleBuilderData.BundleInfo bundleInfo;
                    BundleBuilderData.BundleSplit bundleSplit;
                    BundleBuilderData.BundleSlice bundleSlice;
                    if (TryGetBundleSlice(data, assetBundle, out bundleInfo, out bundleSplit, out bundleSlice))
                    {
                        // Debug.Log(bundleInfo.name);
                        var fileEntry = EncryptFileEntry(data, buildInfo, bundleSplit.encrypted,
                            bundleSlice.name, buildInfo.assetBundlePath);
                        var bundle = new Manifest.BundleInfo();

                        bundle.comment = bundleInfo.note;
                        bundle.tag = bundleInfo.tag;
                        bundle.encrypted = bundleSplit.encrypted;
                        bundle.rsize = fileEntry.rsize;
                        bundle.type = Manifest.BundleType.AssetBundle;
                        bundle.name = fileEntry.name;
                        bundle.checksum = fileEntry.checksum;
                        bundle.size = fileEntry.size;
                        bundle.load = bundleInfo.load;
                        bundle.priority = GetPriority(buildInfo, bundleInfo, bundleSlice);
                        foreach (var assetGuid in bundleSlice.assetGuids)
                        {
                            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            bundle.assets.Add(assetPath);
                        }

                        bundle.dependencies = assetBundleManifest.GetAllDependencies(assetBundle);
                        buildInfo.CreateEntry(bundle);
                        manifest.bundles.Add(bundle);
                        if (bundleSlice.streamingAssets)
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
                    BundleBuilderData.BundleInfo bundleInfo;
                    BundleBuilderData.BundleSplit bundleSplit;
                    BundleBuilderData.BundleSlice bundleSlice;
                    if (TryGetBundleSlice(data, zipArchive.name, out bundleInfo, out bundleSplit, out bundleSlice))
                    {
                        var fileEntry = EncryptFileEntry(data, buildInfo, bundleSplit.encrypted,
                            zipArchive.name, buildInfo.zipArchivePath);
                        var bundle = new Manifest.BundleInfo();

                        bundle.comment = bundleInfo.note;
                        bundle.tag = bundleInfo.tag;
                        bundle.encrypted = false;
                        bundle.rsize = fileEntry.rsize;
                        bundle.type = Manifest.BundleType.ZipArchive;
                        bundle.name = fileEntry.name;
                        bundle.checksum = fileEntry.checksum;
                        bundle.size = fileEntry.size;
                        bundle.load = bundleInfo.load;
                        bundle.priority = GetPriority(buildInfo, bundleInfo, bundleSlice);
                        foreach (var assetPath in zipArchive.assets)
                        {
                            bundle.assets.Add(assetPath);
                        }

                        manifest.bundles.Add(bundle);
                        if (bundleSlice.streamingAssets)
                        {
                            embeddedManifest.bundles.Add(fileEntry);
                        }
                    }
                }
            }

            if (fileListManifest != null)
            {
                foreach (var fileList in fileListManifest.fileLists)
                {
                    var bundleInfo = GetBundleInfo(data, fileList.name);
                    var fileListPath = Path.Combine(buildInfo.packagePath, fileList.name);
                    var fileEntry = GenFileEntry(fileList.name, fileListPath);
                    var bundle = new Manifest.BundleInfo();

                    bundle.comment = bundleInfo.note;
                    bundle.tag = bundleInfo.tag;
                    bundle.type = Manifest.BundleType.FileList;
                    bundle.name = fileList.name;
                    bundle.checksum = fileEntry.checksum;
                    bundle.size = fileEntry.size;
                    bundle.load = bundleInfo.load;
                    bundle.priority = bundleInfo.priority;

                    buildInfo.filelist.Add(fileList.name);
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

            WriteManifest(buildInfo, manifest);
            WriteEmbeddedManifest(buildInfo, embeddedManifest);
            if (buildInfo.buildTarget == EditorUserBuildSettings.activeBuildTarget)
            {
                BuildStreamingAssets(buildInfo, fileListManifest);
            }

            return embeddedManifest;
        }

        // write manifest & checksum of manifest 
        private static void WriteManifest(PackageBuildInfo buildInfo, Manifest manifest)
        {
            var json = JsonUtility.ToJson(manifest);
            var bytes = Encoding.UTF8.GetBytes(json);
            var manifestRawPath = Path.Combine(buildInfo.packagePath, Manifest.ManifestFileName + ".json");
            var manifestChecksumPath = Path.Combine(buildInfo.packagePath, Manifest.ChecksumFileName);
            byte[] zData;
            using (var zStream = new MemoryStream())
            {
                using (var outputStream = new GZipOutputStream(zStream))
                {
                    outputStream.SetLevel(Deflater.BEST_COMPRESSION);
                    outputStream.Write(bytes, 0, bytes.Length);
                    outputStream.Flush();
                }

                zStream.Flush();
                zData = zStream.ToArray();
            }

            buildInfo.filelist.Add(Manifest.ManifestFileName);
            buildInfo.filelist.Add(Manifest.ManifestFileName + ".json");
            var fileEntry = EncryptData(buildInfo, Manifest.ManifestFileName, zData);
            // var manifestPath = Path.Combine(buildInfo.packagePath, Manifest.ManifestFileName);
            // File.WriteAllBytes(manifestPath, zData);
            // var fileEntry = GenFileEntry(Manifest.ManifestFileName, manifestPath);
            var fileEntryJson = JsonUtility.ToJson(fileEntry);
            Debug.LogFormat("write manifest: {0}", fileEntryJson);
            File.WriteAllBytes(manifestRawPath, bytes);
            File.WriteAllText(manifestChecksumPath, fileEntryJson);
        }

        // write embedded manifest to streamingassets 
        private static void WriteEmbeddedManifest(PackageBuildInfo buildInfo, EmbeddedManifest embeddedManifest)
        {
            if (embeddedManifest.bundles.Count > 0)
            {
                var json = JsonUtility.ToJson(embeddedManifest);
                var manifestPath = Path.Combine(buildInfo.packagePath, Manifest.EmbeddedManifestFileName);
                File.WriteAllText(manifestPath, json);
                buildInfo.filelist.Add(Manifest.EmbeddedManifestFileName);
            }
        }

        private static EmbeddedManifest ReadEmbeddedManifest(string packagePath)
        {
            var manifestPath = Path.Combine(packagePath, Manifest.EmbeddedManifestFileName);
            if (File.Exists(manifestPath))
            {
                var text = File.ReadAllText(manifestPath);
                var embeddedManifest = JsonUtility.FromJson<EmbeddedManifest>(text);
                return embeddedManifest;
            }

            return null;
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