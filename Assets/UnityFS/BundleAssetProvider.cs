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
            private bool _disposed;
            private UZipArchiveBundle _bundle;

            public ZipFileSystem(UZipArchiveBundle bundle)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            ~ZipFileSystem()
            {
                _disposed = true;
                JobScheduler.DispatchMain(() => // resurrecting 
                {
                    _bundle.completed -= OnBundleLoaded;
                    _bundle.RemoveRef();
                });
            }

            private void OnBundleLoaded(UBundle bundle)
            {
                if (_disposed)
                {
                    return;
                }
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

            public override Stream OpenRead(string filename)
            {
                return _bundle.OpenRead(filename);
            }
        }

        public class UZipArchiveBundle : UBundle
        {
            private ZipFile _zipFile;
            private BundleAssetProvider _provider;

            public UZipArchiveBundle(BundleAssetProvider provider, Manifest.BundleInfo bundleInfo)
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

            // 打开压缩包中的文件, 返回其文件流
            public Stream OpenRead(string filename)
            {
                if (_zipFile != null)
                {
                    var entry = _zipFile.GetEntry(filename);
                    if (entry != null)
                    {
                        return _zipFile.GetInputStream(entry);
                    }
                }
                return null;
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
                // Debug.Log($"ziparchive loaded {name}");
                if (_IsDependenciesLoaded())
                {
                    OnLoaded();
                }
            }
        }

        // Zip 包中的文件资源
        protected class UZipArchiveBundleAsset : UAsset
        {
            protected UZipArchiveBundle _bundle;

            public UZipArchiveBundleAsset(UZipArchiveBundle bundle, string assetPath)
            : base(assetPath)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    Debug.LogFormat("UZipArchiveBundleAsset {0} released [{1}]", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() => // resurrecting 
                    {
                        ResourceManager.GetAnalyzer().OnAssetClose(_assetPath);
                        _bundle.completed -= OnBundleLoaded;
                        _bundle.RemoveRef();
                    });
                }
            }

            public override byte[] ReadAllBytes()
            {
                return _bundle.ReadAllBytes(_assetPath);
            }

            public Stream OpenRead()
            {
                return _bundle.OpenRead(_assetPath);
            }

            protected virtual void OnBundleLoaded(UBundle bundle)
            {
                if (_disposed)
                {
                    return;
                }
                // _bundle.ReadAllBytes(_assetPath);
                Complete();
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
                    _stream.Close();
                    _stream.Dispose();
                    _stream = null;
                }
                if (_provider != null)
                {
                    _provider.Unload(this);
                    _provider = null;
                }
            }

            // stream 生命周期将被 UAssetBundleBundle 托管
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
                // Debug.Log($"assetbundle loaded {name}");
                if (_IsDependenciesLoaded())
                {
                    OnLoaded();
                }
            }
        }

        // 从 AssetBundle 资源包载入 (不实际调用 assetbundle.LoadAsset)
        protected class UAssetBundleAsset : UAsset
        {
            protected UAssetBundleBundle _bundle;

            public UAssetBundleAsset(UAssetBundleBundle bundle, string assetPath)
            : base(assetPath)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            public override byte[] ReadAllBytes()
            {
                var assetBundle = _bundle.GetAssetBundle();
                if (assetBundle != null)
                {
                    var path = _assetPath;
                    if (!path.EndsWith(".bytes"))
                    {
                        path += ".bytes";
                    }
                    var textAsset = assetBundle.LoadAsset<TextAsset>(path);
                    if (textAsset != null)
                    {
                        return textAsset.bytes;
                    }
                }
                return null;
                // throw new NotSupportedException();
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    Debug.LogFormat("UAssetBundleAsset {0} released [{1}] {2}", _assetPath, bManaged, _bundle.name);
                    _disposed = true;
                    JobScheduler.DispatchMain(() => // resurrecting 
                    {
                        ResourceManager.GetAnalyzer().OnAssetClose(_assetPath);
                        _bundle.completed -= OnBundleLoaded;
                        _bundle.RemoveRef();
                    });
                }
            }

            protected virtual void OnBundleLoaded(UBundle bundle)
            {
                if (_disposed)
                {
                    return;
                }
                Complete();
            }
        }

        // 从 AssetBundle 资源包载入 (会调用 assetbundle.LoadAsset 载入实际资源)
        protected class UAssetBundleConcreteAsset : UAssetBundleAsset
        {
            private Type _type;

            public UAssetBundleConcreteAsset(UAssetBundleBundle bundle, string assetPath, Type type)
            : base(bundle, assetPath)
            {
                _type = type;
            }

            protected override void OnBundleLoaded(UBundle bundle)
            {
                if (_disposed)
                {
                    Complete();
                    return;
                }
                // assert (bundle == _bundle)
                var assetBundle = _bundle.GetAssetBundle();
                if (assetBundle != null)
                {
                    var request = _type != null ? assetBundle.LoadAssetAsync(_assetPath, _type) : assetBundle.LoadAssetAsync(_assetPath);
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
        private List<string> _urls = new List<string>();
        private Func<string, string> _assetPathTransformer;
        private Manifest _manifest;
        private int _slow = 0;
        private int _bufferSize = 0;
        private int _runningTasks = 0;
        private int _concurrentTasks = 0;
        private LinkedList<DownloadTask> _tasks = new LinkedList<DownloadTask>();
        private string _localPathRoot;
        private StreamingAssetsLoader _streamingAssets;
        private bool _disposed;

        private List<Action> _callbacks = new List<Action>();

        public event Action completed
        {
            add
            {
                if (_disposed)
                {
                    Debug.LogError($"BundleAssetProvider already disposed");
                }
                if (_manifest != null)
                {
                    value();
                }
                else
                {
                    _callbacks.Add(value);
                }
            }

            remove
            {
                _callbacks.Remove(value);
            }
        }

        public BundleAssetProvider(string localPathRoot, IList<string> urls, int concurrentTasks, int slow, int bufferSize, Func<string, string> assetPathTransformer)
        {
            _slow = slow;
            _bufferSize = bufferSize;
            _localPathRoot = localPathRoot;
            _assetPathTransformer = assetPathTransformer;
            _urls.AddRange(urls);
            _concurrentTasks = Math.Max(1, Math.Min(concurrentTasks, 4)); // 并发下载任务数量 
        }

        public void AddURLs(params string[] urls)
        {
            _urls.AddRange(urls);
        }

        public void Open()
        {
            Initialize();
        }

        private void Initialize()
        {
            Utils.Helpers.GetManifest(_urls, _localPathRoot, manifest =>
            {
                new StreamingAssetsLoader(manifest).OpenManifest(streamingAssets =>
                {
                    _streamingAssets = streamingAssets;
                    var startups = Utils.Helpers.CollectBundles(manifest, _localPathRoot, bundleInfo =>
                    {
                        if (bundleInfo.startup)
                        {
                            return streamingAssets == null || !streamingAssets.Contains(bundleInfo.name, bundleInfo.checksum, bundleInfo.size);
                        }
                        return false;
                    });
                    if (startups.Length > 0)
                    {
                        for (int i = 0, size = startups.Length; i < size; i++)
                        {
                            var bundleInfo = startups[i];
                            AddDownloadTask(DownloadTask.Create(bundleInfo, _urls, _localPathRoot, -1, 10, self =>
                            {
                                RemoveDownloadTask(self);
                                if (_tasks.Count == 0)
                                {
                                    SetManifest(manifest);
                                    ResourceManager.GetListener().OnSetManifest();
                                }
                            }).SetDebugMode(true), false);
                        }
                        ResourceManager.GetListener().OnStartupTask(startups);
                        Schedule();
                    }
                    else
                    {
                        SetManifest(manifest);
                        ResourceManager.GetListener().OnSetManifest();
                    }
                });
            });
        }

        private string TransformAssetPath(string assetPath)
        {
            return _assetPathTransformer != null ? _assetPathTransformer.Invoke(assetPath) : assetPath;
        }

        private void SetManifest(Manifest manifest)
        {
            _manifest = manifest;
            foreach (var bundle in _manifest.bundles)
            {
                _bundlesMap[bundle.name] = bundle;
                foreach (var assetPath in bundle.assets)
                {
                    _assetPath2Bundle[TransformAssetPath(assetPath)] = bundle.name;
                }
            }
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks[0];
                _callbacks.RemoveAt(0);
                callback();
            }
        }

        // open file stream (file must be listed in manifest)
        private void OpenBundle(UBundle bundle)
        {
            var filename = bundle.name;
            if (LoadBundleFile(bundle, _localPathRoot))
            {
                return;
            }
            if (_streamingAssets != null && _streamingAssets.Contains(bundle.name, bundle.checksum, bundle.size))
            {
                bundle.AddRef();
                JobScheduler.DispatchCoroutine(
                    _streamingAssets.LoadBundle(bundle.name, stream =>
                    {
                        if (stream != null)
                        {
                            bundle.Load(stream);
                        }
                        else
                        {
                            PrintLog($"read from streamingassets failed: {bundle.name}");
                            DownloadBundleFile(bundle);
                        }
                        bundle.RemoveRef();
                    })
                );
                return;
            }
            DownloadBundleFile(bundle);
        }

        private void DownloadBundleFile(UBundle bundle)
        {
            // 无法打开现有文件, 下载新文件
            bundle.AddRef();
            AddDownloadTask(DownloadTask.Create(bundle.bundleInfo, _urls, _localPathRoot, -1, 10, self =>
            {
                RemoveDownloadTask(self);
                if (!LoadBundleFile(bundle, _localPathRoot))
                {
                    bundle.Load(null);
                }
                bundle.RemoveRef();
            }).SetDebugMode(true), true);
        }

        // 检查是否存在有效的本地包
        public bool IsBundleFileValid(Manifest.BundleInfo bundleInfo)
        {
            var fullPath = Path.Combine(_localPathRoot, bundleInfo.name);
            return Utils.Helpers.IsBundleFileValid(fullPath, bundleInfo);
        }

        // 检查是否存在有效的本地包
        public bool IsBundleFileValid(string bundleName)
        {
            Manifest.BundleInfo bundleInfo;
            if (_bundlesMap.TryGetValue(bundleName, out bundleInfo))
            {
                return IsBundleFileValid(bundleInfo);
            }
            return false;
        }

        private bool LoadBundleFile(UBundle bundle, string localPathRoot)
        {
            var fullPath = Path.Combine(localPathRoot, bundle.name);
            var fileStream = Utils.Helpers.GetBundleStream(fullPath, bundle.bundleInfo);
            if (fileStream != null)
            {
                bundle.Load(fileStream); // 生命周期转由 UAssetBundleBundle 管理
                return true;
            }
            return false;
        }

        private void PrintException(Exception exception)
        {
            Debug.LogError(exception);
        }

        private void PrintLog(string message)
        {
            Debug.Log(message);
        }

        protected void Unload(UBundle bundle)
        {
            _bundles.Remove(bundle.name);
            PrintLog($"bundle unloaded: {bundle.name}");
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

        public void ForEachTask(Action<ITask> callback)
        {
            for (var node = _tasks.First; node != null; node = node.Next)
            {
                var task = node.Value;
                callback(task);
            }
        }

        private DownloadTask AddDownloadTask(DownloadTask newTask, bool bSchedule)
        {
            for (var node = _tasks.First; node != null; node = node.Next)
            {
                var task = node.Value;
                if (!task.isRunning && !task.isDone)
                {
                    if (newTask.priority > task.priority)
                    {
                        _tasks.AddAfter(node, newTask);
                        Schedule();
                        return newTask;
                    }
                }
            }
            _tasks.AddLast(newTask);
            if (bSchedule)
            {
                Schedule();
            }
            return newTask;
        }

        private void RemoveDownloadTask(DownloadTask task)
        {
            _tasks.Remove(task);
            _runningTasks--;
            ResourceManager.GetListener().OnTaskComplete(task);
            Schedule();
        }

        private void Schedule()
        {
            if (_runningTasks >= _concurrentTasks)
            {
                return;
            }

            for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
            {
                var task = taskNode.Value;
                if (!task.isRunning && !task.isDone)
                {
                    _runningTasks++;
                    task.slow = _slow;
                    task.bufferSize = _bufferSize;
                    task.Run();
                    ResourceManager.GetListener().OnTaskStart(task);
                    break;
                }
            }
        }

        public void Close()
        {
            Abort();
            OnRelease();
        }

        private void OnRelease()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.Collect();
                var count = _bundles.Count;
                if (count > 0)
                {
                    var bundles = new UBundle[count];
                    _bundles.Values.CopyTo(bundles, 0);
                    for (var i = 0; i < count; i++)
                    {
                        var bundle = bundles[i];
                        // PrintLog($"关闭管理器, 强制释放资源包 {bundle.name}");
                        bundle.Release();
                    }
                }
            }
        }

        // 终止所有任务
        public void Abort()
        {
            for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
            {
                var task = taskNode.Value;
                task.Abort();
            }
            _tasks.Clear();
        }

        public UBundle GetBundle(string bundleName)
        {
            UBundle bundle;
            if (!_bundles.TryGetValue(bundleName, out bundle))
            {
                Manifest.BundleInfo bundleInfo;
                if (_bundlesMap.TryGetValue(bundleName, out bundleInfo))
                {
                    switch (bundleInfo.type)
                    {
                        case Manifest.BundleType.AssetBundle:
                            bundle = new UAssetBundleBundle(this, bundleInfo);
                            break;
                        case Manifest.BundleType.ZipArchive:
                            bundle = new UZipArchiveBundle(this, bundleInfo);
                            break;
                    }

                    if (bundle != null)
                    {
                        _bundles.Add(bundleName, bundle);
                        _AddDependencies(bundle, bundle.bundleInfo.dependencies);
                        OpenBundle(bundle);
                    }
                }
            }
            return bundle;
        }

        public UScene LoadScene(string assetPath)
        {
            return new UScene(GetAsset(assetPath, false, null)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UScene(GetAsset(assetPath, false, null)).LoadAdditive();
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
                var zipArchiveBundle = bundle as UZipArchiveBundle;
                if (zipArchiveBundle != null)
                {
                    fileSystem = new ZipFileSystem(zipArchiveBundle);
                    _fileSystems[bundleName] = new WeakReference(fileSystem);
                }
                else
                {
                    Debug.LogError($"bundle {bundleName} ({bundle.GetType()}) type error.");
                }
                bundle.RemoveRef();
                if (fileSystem != null)
                {
                    return fileSystem;
                }
            }
            var invalid = new FailureFileSystem(bundleName);
            _fileSystems[bundleName] = new WeakReference(invalid);
            return invalid;
        }

        public UAsset GetAsset(string assetPath, Type type)
        {
            return GetAsset(assetPath, true, type);
        }

        public string Find(string assetPath)
        {
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(TransformAssetPath(assetPath), out bundleName))
            {
                return bundleName;
            }
            return null;
        }

        private UAsset GetAsset(string assetPath, bool concrete, Type type)
        {
            UAsset asset = null;
            WeakReference assetRef;
            var transformedAssetPath = TransformAssetPath(assetPath);
            if (_assets.TryGetValue(transformedAssetPath, out assetRef) && assetRef.IsAlive)
            {
                asset = assetRef.Target as UAsset;
                if (asset != null)
                {
                    ResourceManager.GetAnalyzer().OnAssetAccess(assetPath);
                    return asset;
                }
            }
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(transformedAssetPath, out bundleName))
            {
                var bundle = this.GetBundle(bundleName);
                if (bundle != null)
                {
                    bundle.AddRef();
                    var assetBundleUBundle = bundle as UAssetBundleBundle;
                    if (assetBundleUBundle != null)
                    {
                        ResourceManager.GetAnalyzer().OnAssetOpen(assetPath);
                        if (concrete)
                        {
                            asset = new UAssetBundleConcreteAsset(assetBundleUBundle, assetPath, type);
                        }
                        else
                        {
                            asset = new UAssetBundleAsset(assetBundleUBundle, assetPath);
                        }
                        _assets[TransformAssetPath(assetPath)] = new WeakReference(asset);
                    }
                    else
                    {
                        var zipArchiveBundle = bundle as UZipArchiveBundle;
                        if (zipArchiveBundle != null)
                        {
                            ResourceManager.GetAnalyzer().OnAssetOpen(assetPath);
                            asset = new UZipArchiveBundleAsset(zipArchiveBundle, assetPath);
                            _assets[TransformAssetPath(assetPath)] = new WeakReference(asset);
                        }
                    }
                    bundle.RemoveRef();
                    return asset;
                }
                // 不是 Unity 资源包, 不能实例化 AssetBundleUAsset
            }
            var invalid = new UFailureAsset(assetPath);
            _assets[TransformAssetPath(assetPath)] = new WeakReference(invalid);
            return invalid;
        }
    }
}
