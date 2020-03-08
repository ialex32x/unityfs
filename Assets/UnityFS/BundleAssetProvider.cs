using System;
using System.IO;
using System.Collections;
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
    public partial class BundleAssetProvider : IAssetProvider
    {
        // 资源路径 => 资源包 的快速映射
        private Dictionary<string, string> _assetPath2Bundle = new Dictionary<string, string>();
        private Dictionary<string, Manifest.BundleInfo> _bundlesMap = new Dictionary<string, Manifest.BundleInfo>();
        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();
        private Dictionary<string, WeakReference> _fileSystems = new Dictionary<string, WeakReference>();
        private Dictionary<string, UBundle> _bundles = new Dictionary<string, UBundle>();
        private Func<string, string> _assetPathTransformer;
        private Manifest _manifest;
        private int _slow = 0;
        private int _bufferSize = 0;
        private bool _backgroundSchedule = false;
        private int _activeTasks = 0; // 运行中的前台任务
        private int _concurrentTasks = 0; // 可并发数量
        private LinkedList<DownloadTask> _tasks = new LinkedList<DownloadTask>();
        private LinkedList<Manifest.BundleInfo> _backgroundQueue = new LinkedList<Manifest.BundleInfo>();
        private LinkedList<IEnumerator> _bundleLoaders = new LinkedList<IEnumerator>();
        private LinkedList<IEnumerator> _assetLoaders = new LinkedList<IEnumerator>();
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

        public BundleAssetProvider(string localPathRoot, int concurrentTasks, int slow, int bufferSize, Func<string, string> assetPathTransformer)
        {
            _slow = slow;
            _bufferSize = bufferSize;
            _localPathRoot = localPathRoot;
            _assetPathTransformer = assetPathTransformer;
            _concurrentTasks = Math.Max(2, Math.Min(concurrentTasks, 4)); // 并发下载任务数量 
        }

        public void Open(ResourceManagerArgs args)
        {
            Utils.Helpers.GetManifest(_localPathRoot, args.manifestChecksum, args.manifestSize, manifest =>
            {
                new StreamingAssetsLoader(manifest).OpenManifest(streamingAssets =>
                {
                    _streamingAssets = streamingAssets;
                    var startups = new List<Manifest.BundleInfo>();
                    var startupTasks = new List<DownloadTask>();
                    for (int i = 0, size = manifest.bundles.Count; i < size; i++)
                    {
                        var bundleInfo = manifest.bundles[i];

                        if (streamingAssets == null || !streamingAssets.Contains(bundleInfo.name, bundleInfo.checksum, bundleInfo.size))
                        {
                            // streamingAssets 未命中
                            if (streamingAssets == null || !streamingAssets.Contains(bundleInfo.name, bundleInfo.checksum, bundleInfo.size))
                            {
                                var fullPath = Path.Combine(_localPathRoot, bundleInfo.name);
                                // 本地存储 未命中
                                if (!Utils.Helpers.IsBundleFileValid(fullPath, bundleInfo))
                                {
                                    if (bundleInfo.startup)
                                    {
                                        startups.Add(bundleInfo);
                                        var task = DownloadTask.Create(bundleInfo, fullPath, -1, 10, self =>
                                        {
                                            RemoveDownloadTask(self, true);
                                            if (startupTasks.Remove(self))
                                            {
                                                if (startupTasks.Count == 0)
                                                {
                                                    SetManifest(manifest);
                                                    ResourceManager.GetListener().OnSetManifest();
                                                }
                                            }
                                        });
                                        startupTasks.Add(task);
                                        AddDownloadTask(task, false);
                                    }
                                    else
                                    {
                                        if (bundleInfo.load != Manifest.BundleLoad.Optional)
                                        {
                                            var added = false;
                                            var node = _backgroundQueue.First;
                                            while (node != null)
                                            {
                                                if (bundleInfo.priority > node.Value.priority)
                                                {
                                                    added = true;
                                                    _backgroundQueue.AddBefore(node, bundleInfo);
                                                    break;
                                                }
                                                node = node.Next;
                                            }
                                            if (!added)
                                            {
                                                _backgroundQueue.AddLast(bundleInfo);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (startups.Count > 0)
                    {
                        ResourceManager.GetListener().OnStartupTask(startups.ToArray());
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

        protected void _LoadBundle(IEnumerator e)
        {
            _bundleLoaders.AddLast(e);
            if (_bundleLoaders.Count == 1)
            {
                JobScheduler.DispatchCoroutine(_BundleLoader());
            }
        }

        protected void _LoadAsset(IEnumerator e)
        {
            _assetLoaders.AddLast(e);
            if (_assetLoaders.Count == 1)
            {
                JobScheduler.DispatchCoroutine(_AssetLoader());
            }
        }

        private IEnumerator _BundleLoader()
        {
            while (true)
            {
                var first = _bundleLoaders.First;
                if (first == null)
                {
                    break;
                }
                var value = first.Value;
                while (value.MoveNext())
                {
                    yield return value.Current;
                }
                _bundleLoaders.Remove(first);
            }
        }

        private IEnumerator _AssetLoader()
        {
            while (true)
            {
                var first = _assetLoaders.First;
                if (first == null)
                {
                    break;
                }
                var value = first.Value;
                while (value.MoveNext())
                {
                    yield return value.Current;
                }
                _assetLoaders.Remove(first);
            }
        }

        private string TransformAssetPath(string assetPath)
        {
            return _assetPathTransformer != null ? _assetPathTransformer.Invoke(assetPath) : assetPath;
        }

        private void SetManifest(Manifest manifest)
        {
            _manifest = manifest;
            _assetPath2Bundle.Clear();
            _bundlesMap.Clear();
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
            IdleSchedule(120f);
        }

        // open file stream (file must be listed in manifest)
        private void OpenBundle(UBundle bundle)
        {
            var filename = bundle.name;
            if (LoadBundleFile(bundle))
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
                            Debug.LogWarningFormat("read from streamingassets failed: {0}", bundle.name);
                            DownloadBundleFile(bundle, true);
                        }
                        bundle.RemoveRef();
                    })
                );
                return;
            }
            DownloadBundleFile(bundle, true);
        }

        private void DownloadBundleFile(UBundle bundle, bool bForeground)
        {
            // 无法打开现有文件, 下载新文件
            bundle.AddRef();
            var bundlePath = Path.Combine(_localPathRoot, bundle.name);
            AddDownloadTask(DownloadTask.Create(bundle.bundleInfo, bundlePath, -1, 10, self =>
            {
                RemoveDownloadTask(self, bForeground);
                if (!LoadBundleFile(bundle))
                {
                    bundle.Load(null);
                }
                bundle.RemoveRef();
            }).SetDebugMode(true), true);
        }

        // 检查是否存在有效的本地包
        public bool IsBundleAvailable(Manifest.BundleInfo bundleInfo)
        {
            var fullPath = Path.Combine(_localPathRoot, bundleInfo.name);
            return Utils.Helpers.IsBundleFileValid(fullPath, bundleInfo);
        }

        public bool IsFileAvailable(FileEntry fileEntry)
        {
            var fullPath = Path.Combine(_localPathRoot, fileEntry.name);
            return Utils.Helpers.IsFileValid(fullPath, fileEntry);
        }

        // 检查是否存在有效的本地包
        public bool IsBundleAvailable(string bundleName)
        {
            Manifest.BundleInfo bundleInfo;
            if (_bundlesMap.TryGetValue(bundleName, out bundleInfo))
            {
                return IsBundleAvailable(bundleInfo);
            }
            return false;
        }

        private bool LoadBundleFile(UBundle bundle)
        {
            var fullPath = Path.Combine(_localPathRoot, bundle.name);
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

        protected void Unload(UBundle bundle)
        {
            _bundles.Remove(bundle.name);
            // Debug.LogFormat("bundle unloaded: {0}", bundle.name);
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
                        _tasks.AddBefore(node, newTask);
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

        // bForeground: 标记为前台
        private void RemoveDownloadTask(DownloadTask task, bool bForeground)
        {
            _tasks.Remove(task);
            _activeTasks--;
            ResourceManager.GetListener().OnTaskComplete(task);
            Schedule();
        }

        //TODO: 待优化, 已经存在相同目标文件的任务时, 不再创建
        private bool IsSameTaskRunning(DownloadTask expectedTask)
        {
            for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
            {
                var task = taskNode.Value;
                if (task != expectedTask && task.path == expectedTask.path && task.isRunning)
                {
                    return true;
                }
            }
            return false;
        }

        private void Schedule()
        {
            if (_activeTasks >= _concurrentTasks)
            {
                return;
            }
            for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
            {
                var task = taskNode.Value;
                if (!task.isRunning && !task.isDone && !IsSameTaskRunning(task))
                {
                    _activeTasks++;
                    task.slow = _slow;
                    task.bufferSize = _bufferSize;
                    task.Run();
                    ResourceManager.GetListener().OnTaskStart(task);
                    return;
                }
            }
            // no more task
            IdleSchedule(15f);
        }

        private void IdleSchedule(float delay)
        {
            // Debug.LogWarningFormat("no more task {0}, {1}", _backgroundQueue.Count, _backgroundSchedule);
            if (_backgroundQueue.Count > 0 && !_backgroundSchedule)
            {
                _backgroundSchedule = true;
                JobScheduler.DispatchAfter(DoIdleSchedule, delay);
            }
        }

        private void DoIdleSchedule()
        {
            // Debug.LogWarningFormat("IdleSchedule.enter");
            _backgroundSchedule = false;
            if (_activeTasks != 0)
            {
                // Debug.LogWarningFormat("IdleSchedule.activeTask {0} ({1})", _activeTasks, _backgroundQueue.Count);
                return;
            }
            var node = _backgroundQueue.First;
            if (node != null)
            {
                var bundleInfo = node.Value;
                _backgroundQueue.Remove(node);
                if (!IsBundleAvailable(bundleInfo))
                {
                    var bundlePath = Path.Combine(_localPathRoot, bundleInfo.name);
                    var idleTask = DownloadTask.Create(bundleInfo, bundlePath, -1, 10, self =>
                    {
                        // Debug.LogWarningFormat("idle task complete {0}", self.path);
                        RemoveDownloadTask(self, true);
                    }).SetDebugMode(true);
                    _tasks.AddLast(idleTask);
                    _activeTasks++;
                    idleTask.slow = _slow;
                    idleTask.bufferSize = _bufferSize;
                    // Debug.LogWarningFormat("idle task start {0}", idleTask.path);
                    idleTask.Run();
                    ResourceManager.GetListener().OnTaskStart(idleTask);
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

        // 获取包信息
        public Manifest.BundleInfo GetBundleInfo(string bundleName)
        {
            Manifest.BundleInfo bundleInfo;
            if (_bundlesMap.TryGetValue(bundleName, out bundleInfo))
            {
                return bundleInfo;
            }
            return null;
        }

        // 获取包对象 (会直接进入加载队列)
        public UBundle GetBundle(string bundleName)
        {
            var bundleInfo = GetBundleInfo(bundleName);
            return GetBundle(bundleInfo);
        }

        public UBundle GetBundle(Manifest.BundleInfo bundleInfo)
        {
            UBundle bundle = null;
            if (bundleInfo != null)
            {
                _backgroundQueue.Remove(bundleInfo);
                var bundleName = bundleInfo.name;
                if (!_bundles.TryGetValue(bundleName, out bundle))
                {
                    switch (bundleInfo.type)
                    {
                        case Manifest.BundleType.AssetBundle:
                            bundle = new UAssetBundleBundle(this, bundleInfo);
                            break;
                        case Manifest.BundleType.ZipArchive:
                            bundle = new UZipArchiveBundle(this, bundleInfo);
                            break;
                        case Manifest.BundleType.FileList:
                            bundle = new UFileListBundle(this, bundleInfo);
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

        // 检查资源是否本地直接可用
        public bool IsAssetAvailable(string assetPath)
        {
            WeakReference assetRef;
            var transformedAssetPath = TransformAssetPath(assetPath);
            if (_assets.TryGetValue(transformedAssetPath, out assetRef) && assetRef.IsAlive)
            {
                var asset = assetRef.Target as UAsset;
                if (asset != null && asset.isAvailable)
                {
                    return true;
                }
            }
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(transformedAssetPath, out bundleName))
            {
                var bundleInfo = GetBundleInfo(bundleName);
                if (bundleInfo != null)
                {
                    var bundleType = bundleInfo.type;
                    if (bundleType == Manifest.BundleType.FileSystem)
                    {
                        // var fileEntry = lookup file entry by assetPath in filesystem bundle (info);
                        // return IsFileAvailable(fileEntry);
                        throw new NotImplementedException();
                    }
                    return IsBundleAvailable(bundleInfo);
                }
            }
            return false;
        }

        public bool IsAssetExists(string assetPath)
        {
            return Find(assetPath) != null;
        }

        // 查找资源 assetPath 对应的 bundle.name
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
                    ResourceManager.GetAnalyzer()?.OnAssetAccess(assetPath);
                    return asset;
                }
            }
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(transformedAssetPath, out bundleName))
            {
                var bundle = this.GetBundle(bundleName);
                if (bundle != null)
                {
                    try
                    {
                        bundle.AddRef();
                        ResourceManager.GetAnalyzer()?.OnAssetOpen(assetPath);
                        asset = bundle.CreateAsset(assetPath, type, concrete);
                        if (asset != null)
                        {
                            _assets[TransformAssetPath(assetPath)] = new WeakReference(asset);
                            return asset;
                        }
                    }
                    finally
                    {
                        bundle.RemoveRef();
                    }
                }
                // 不是 Unity 资源包, 不能实例化 AssetBundleUAsset
            }
            var invalid = new UFailureAsset(assetPath);
            _assets[TransformAssetPath(assetPath)] = new WeakReference(invalid);
            return invalid;
        }
    }
}
