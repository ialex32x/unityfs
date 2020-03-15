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
        private int _activeJobs = 0;
        private DownloadWorker _downloadWorker;

        // 正在进行的下载任务
        private LinkedList<DownloadWorker.JobInfo> _tasks = new LinkedList<DownloadWorker.JobInfo>();

        private LinkedList<Manifest.BundleInfo> _backgroundQueue = new LinkedList<Manifest.BundleInfo>();
        private LinkedList<IEnumerator> _bundleLoaders = new LinkedList<IEnumerator>();
        private LinkedList<IEnumerator> _assetLoaders = new LinkedList<IEnumerator>();
        private string _localPathRoot;
        private StreamingAssetsLoader _streamingAssets;
        private bool _disposed;
        private string _password;

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

            remove => _callbacks.Remove(value);
        }

        public BundleAssetProvider(string localPathRoot, int loopLatency, int bufferSize,
            Func<string, string> assetPathTransformer)
        {
            _downloadWorker = new DownloadWorker(onDownloadJobDone, bufferSize, loopLatency,
                System.Threading.ThreadPriority.BelowNormal);
            _localPathRoot = localPathRoot;
            _assetPathTransformer = assetPathTransformer;
        }

        public void Open(ResourceManagerArgs args)
        {
            _password = args.password;
            Utils.Helpers.GetManifest(_localPathRoot, args.manifestChecksum, args.manifestSize, args.manifestRSize,
                _password, manifest =>
                {
                    _streamingAssets = new StreamingAssetsLoader();
                    _streamingAssets.LoadEmbeddedManifest(streamingAssets =>
                    {
                        SetManifest(manifest);
                        ResourceManager.GetListener().OnSetManifest();
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
            return _bundlesMap.TryGetValue(bundleName, out var bundleInfo) && IsBundleAvailable(bundleInfo);
        }

        private bool LoadBundleFile(UBundle bundle)
        {
            var fullPath = Path.Combine(_localPathRoot, bundle.name);
            var fileStream = Utils.Helpers.GetBundleStream(fullPath, bundle.bundleInfo);
            if (fileStream != null)
            {
                // Stream 生命周期转由 UAssetBundleBundle 管理
                bundle.Load(Utils.Helpers.GetDecryptStream(fileStream, bundle.bundleInfo, _password));
                return true;
            }

            return false;
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
            _tasks.Clear();
            _downloadWorker.Abort();
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

        // 尝试获取包对象 (不会自动创建并加载)
        public UBundle TryGetBundle(Manifest.BundleInfo bundleInfo)
        {
            return bundleInfo != null && _bundles.TryGetValue(bundleInfo.name, out var bundle) ? bundle : null;
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
                        // 第一次访问 UBundle 时进入此处逻辑, 但这之前可能已经使用 EnsureBundles 等方式发起文件下载
                        // 优先检查是否已经存在下载中的任务, 如果已经存在下载任务, 任务完成时将自动调用 UBundle.Load(stream)
                        var bundleJob = _FindDownloadJob(bundle.name);
                        if (bundleJob == null)
                        {
                            if (!LoadBundleFile(bundle))
                            {
                                DownloadBundleFile(bundle.bundleInfo, null);
                            }
                        }
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