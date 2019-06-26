using System;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS
{
    using UnityEngine;

    /** 
    资源包资源管理

    主要接口: 
    UBundle GetBundle(string bundleName)
    IFileSystem GetFileSystem(string bundleName)
    UAsset GetAsset(string assetPath)
    */
    public class BundleAssetProvider : IAssetProvider
    {
        public class ZipFileSystem : AbstractFileSystem
        {
            private ZipArchiveUBundle _bundle;

            public ZipFileSystem(ZipArchiveUBundle bundle)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            ~ZipFileSystem()
            {
                JobScheduler.DispatchMain(() =>
                {
                    _bundle.completed -= OnBundleLoaded;
                    _bundle.RemoveRef();
                });
            }

            private void OnBundleLoaded(UBundle bundle)
            {
                Complete();
            }

            public override bool Exists(string filename)
            {
                return _bundle.Exists(filename);
            }

            public override byte[] ReadAllBytes(string filename)
            {
                return _bundle.ReadAllBytes(filename);
            }
        }

        public class ZipArchiveUBundle : UBundle
        {
            private ZipFile _zipFile;
            private BundleAssetProvider _provider;

            public ZipArchiveUBundle(BundleAssetProvider provider, Manifest.BundleInfo bundleInfo)
            : base(bundleInfo)
            {
                _provider = provider;
            }

            protected override void OnRelease()
            {
                base.OnRelease();
                if (_zipFile != null)
                {
                    _zipFile.Close();
                    _zipFile = null;
                }
                _provider.Unload(this);
            }

            public bool Exists(string filename)
            {
                if (_zipFile != null)
                {
                    var entry = _zipFile.FindEntry(filename, false);
                    if (entry >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            public byte[] ReadAllBytes(string filename)
            {
                if (_zipFile != null)
                {
                    var entry = _zipFile.GetEntry(filename);
                    if (entry != null)
                    {
                        using (var stream = _zipFile.GetInputStream(entry))
                        {
                            var buffer = new byte[entry.Size];
                            stream.Read(buffer, 0, buffer.Length);
                            return buffer;
                        }
                    }
                }
                return null;
            }

            public override void Load(Stream stream)
            {
                _zipFile = new ZipFile(stream);
                _zipFile.IsStreamOwner = true;
                _loaded = true;
                if (_IsDependenciesLoaded())
                {
                    OnLoaded();
                }
            }
        }

        // AssetBundle 资源包
        protected class UAssetBundleBundle : UBundle
        {
            private Stream _stream; // manage the stream lifecycle (dispose after assetbundle.unload)
            private AssetBundle _assetBundle;
            private BundleAssetProvider _provider;

            public UAssetBundleBundle(BundleAssetProvider provider, Manifest.BundleInfo bundleInfo)
            : base(bundleInfo)
            {
                _provider = provider;
            }

            protected override void OnRelease()
            {
                base.OnRelease();
                if (_assetBundle != null)
                {
                    _assetBundle.Unload(true);
                    _assetBundle = null;
                }
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
                _provider.Unload(this);
            }

            public override void Load(Stream stream)
            {
                _stream = stream;
                var request = AssetBundle.LoadFromStreamAsync(stream);
                request.completed += OnAssetBundleLoaded;
            }

            public AssetBundle GetAssetBundle()
            {
                return _assetBundle;
            }

            private void OnAssetBundleLoaded(AsyncOperation op)
            {
                var request = op as AssetBundleCreateRequest;
                if (request != null)
                {
                    _assetBundle = request.assetBundle;
                }
                _loaded = true;
                if (_IsDependenciesLoaded())
                {
                    OnLoaded();
                }
            }
        }

        // 从 AssetBundle 资源包载入资源
        protected class UAssetBundleAsset : UAsset
        {
            private UAssetBundleBundle _bundle;

            public UAssetBundleAsset(UAssetBundleBundle bundle, string assetPath)
            : base(assetPath)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            ~UAssetBundleAsset()
            {
                JobScheduler.DispatchMain(() =>
                {
                    _bundle.completed -= OnBundleLoaded;
                    _bundle.RemoveRef();
                });
            }

            private void OnBundleLoaded(UBundle bundle)
            {
                // assert (bundle == _bundle)
                var assetBundle = _bundle.GetAssetBundle();
                if (assetBundle != null)
                {
                    var request = assetBundle.LoadAssetAsync(_assetPath);
                    request.completed += OnAssetLoaded;
                }
                else
                {
                    Complete(); // failed
                }
            }

            private void OnAssetLoaded(AsyncOperation op)
            {
                var request = op as AssetBundleRequest;
                _object = request.asset;
                Complete();
            }
        }

        // 资源路径 => 资源包 的快速映射
        private Dictionary<string, string> _assetPath2Bundle = new Dictionary<string, string>();
        private Dictionary<string, Manifest.BundleInfo> _bundlesMap = new Dictionary<string, Manifest.BundleInfo>();
        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();
        private Dictionary<string, WeakReference> _fileSystems = new Dictionary<string, WeakReference>();
        private Dictionary<string, UBundle> _bundles = new Dictionary<string, UBundle>();
        private IList<string> _urls;
        private Manifest _manifest;
        private int _runningTasks = 0;
        private int _concurrentTasks = 3;
        private LinkedList<DownloadTask> _tasks = new LinkedList<DownloadTask>();
        private string _localPathRoot;

        public BundleAssetProvider(Manifest manifest, string localPathRoot, IList<string> urls)
        {
            _manifest = manifest;
            _localPathRoot = localPathRoot;
            _urls = urls;
            this.Initialize();
        }

        private void Initialize()
        {
            foreach (var bundle in _manifest.bundles)
            {
                _bundlesMap[bundle.name] = bundle;
                foreach (var assetPath in bundle.assets)
                {
                    _assetPath2Bundle[assetPath] = bundle.name;
                }
            }
        }

        // open file stream (file must be listed in manifest)
        private Stream OpenFile(string filename)
        {
            Manifest.BundleInfo bundleInfo;
            if (_bundlesMap.TryGetValue(filename, out bundleInfo))
            {
                var fullPath = Path.Combine(_localPathRoot, filename);
                var metaPath = fullPath + Metadata.Ext;
                if (File.Exists(fullPath) && File.Exists(metaPath))
                {
                    var json = File.ReadAllText(metaPath);
                    var metadata = JsonUtility.FromJson<Metadata>(json);
                    // quick but unsafe
                    if (metadata.checksum == bundleInfo.checksum && metadata.size == bundleInfo.size)
                    {
                        var stream = System.IO.File.OpenRead(fullPath);
                        return stream;
                    }
                }
            }
            return null;
        }

        protected void Unload(UBundle bundle)
        {
            _bundles.Remove(bundle.name);
        }

        private void _AddDependencies(UBundle bundle, string[] dependencies)
        {
            if (dependencies != null)
            {
                for (int i = 0, size = dependencies.Length; i < size; i++)
                {
                    var depBundleInfo = dependencies[i];
                    var depBundle = GetBundle(depBundleInfo);
                    if (bundle.AddDependency(depBundle))
                    {
                        _AddDependencies(bundle, depBundle.bundleInfo.dependencies);
                    }
                }
            }
        }

        private void AddDownloadTask(DownloadTask newTask)
        {
            for (var node = _tasks.First; node != null; node = node.Next)
            {
                var task = node.Value;
                if (!task.isRunning && !task.isDone)
                {
                    if (newTask.bundleInfo.priority > task.bundleInfo.priority)
                    {
                        _tasks.AddAfter(node, newTask);
                        Schedule();
                        return;
                    }
                }
            }
            _tasks.AddLast(newTask);
            Schedule();
        }

        private void Schedule()
        {
            if (_runningTasks >= _concurrentTasks)
            {
                return;
            }

            for (var node = _tasks.First; node != null; node = node.Next)
            {
                var task = node.Value;
                if (!task.isRunning && !task.isDone)
                {
                    _runningTasks++;
                    task.Run();
                    break;
                }
            }
        }

        public UBundle GetBundle(string bundleName)
        {
            UBundle bundle;
            if (!_bundles.TryGetValue(bundleName, out bundle))
            {
                var bundleInfo = _bundlesMap[bundleName];
                switch (bundleInfo.type)
                {
                    case Manifest.BundleType.AssetBundle:
                        bundle = new UAssetBundleBundle(this, bundleInfo);
                        break;
                    case Manifest.BundleType.ZipArchive:
                        bundle = new ZipArchiveUBundle(this, bundleInfo);
                        break;
                }

                if (bundle != null)
                {
                    _bundles.Add(bundleName, bundle);
                    _AddDependencies(bundle, bundle.bundleInfo.dependencies);
                    var fs = OpenFile(bundleName);
                    if (fs != null)
                    {
                        bundle.Load(fs);
                    }
                    else
                    {
                        AddDownloadTask(DownloadTask.Create(bundle, _urls, -1, _localPathRoot, self =>
                        {
                            _tasks.Remove(self);
                            _runningTasks--;
                            Schedule();
                            bundle.Load(self.OpenFile());
                        }));
                    }
                }
            }
            return bundle;
        }

        public IFileSystem GetFileSystem(string bundleName)
        {
            IFileSystem fileSystem = null;
            WeakReference fileSystemRef;
            if (_fileSystems.TryGetValue(bundleName, out fileSystemRef))
            {
                fileSystem = fileSystemRef.Target as IFileSystem;
                if (fileSystem != null)
                {
                    return fileSystem;
                }
            }
            var bundle = this.GetBundle(bundleName);
            if (bundle != null)
            {
                bundle.AddRef();
                var zipArchiveBundle = bundle as ZipArchiveUBundle;
                if (zipArchiveBundle != null)
                {
                    fileSystem = new ZipFileSystem(zipArchiveBundle);
                    _fileSystems[bundleName] = new WeakReference(fileSystem);
                }
                bundle.RemoveRef();
                return fileSystem;
            }
            return null;
        }

        public UAsset GetAsset(string assetPath)
        {
            UAsset asset = null;
            WeakReference assetRef;
            if (_assets.TryGetValue(assetPath, out assetRef) && assetRef.IsAlive)
            {
                asset = assetRef.Target as UAsset;
                if (asset != null)
                {
                    return asset;
                }
            }
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(assetPath, out bundleName))
            {
                var bundle = this.GetBundle(bundleName);
                if (bundle != null)
                {
                    bundle.AddRef();
                    var assetBundleUBundle = bundle as UAssetBundleBundle;
                    if (assetBundleUBundle != null)
                    {
                        asset = new UAssetBundleAsset(assetBundleUBundle, assetPath);
                        _assets[assetPath] = new WeakReference(asset);
                    }
                    bundle.RemoveRef();
                    return asset;
                }
                // 不是 Unity 资源包, 不能实例化 AssetBundleUAsset
            }
            var invalid = new FailureUAsset(assetPath);
            _assets[assetPath] = new WeakReference(invalid);
            return invalid;
        }
    }
}
