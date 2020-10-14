using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine.Networking;
using UnityFS.Utils;

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
        private ManifestEntry _manifestFileEntry; // 清单自身信息
        private Manifest _manifestObject; // 当前清单 
        private int _activeJobs = 0;
        private int _bytesPerSecond;
        private int _bytesPerSecondIdle;
        private DownloadWorker _worker; // 必要资源下载工作线程 (请求使用的资源)
        private DownloadWorker _idleWorker; // 空闲下载工作线程 

        // 正在进行的下载任务
        private LinkedList<DownloadWorker.JobInfo> _jobs = new LinkedList<DownloadWorker.JobInfo>();

        private LinkedList<IEnumerator> _bundleLoaders = new LinkedList<IEnumerator>();
        private LinkedList<IEnumerator> _assetLoaders = new LinkedList<IEnumerator>();
        private string _localPathRoot;
        private StreamingAssetsLoader _streamingAssets;
        private bool _closed;
        private string _password;

        private List<Action> _callbacks = new List<Action>();

        public string tag => _manifestObject?.tag;

        public int build => _manifestObject != null ? _manifestObject.build : -1;

        public event Action completed
        {
            add
            {
                if (_closed)
                {
                    Debug.LogError($"BundleAssetProvider already disposed");
                }

                if (_manifestObject != null)
                {
                    value();
                }
                else
                {
                    _callbacks.Add(value);
                }
            }

            remove { _callbacks.Remove(value); }
        }

        public BundleAssetProvider()
        {
        }

        public void Open(ResourceManagerArgs args)
        {
            _bytesPerSecond = args.bytesPerSecond;
            _bytesPerSecondIdle = args.bytesPerSecondIdle;
            _worker = new DownloadWorker(onDownloadJobDone, args.bufferSize, args.urls,
                System.Threading.ThreadPriority.BelowNormal);
            _idleWorker = new DownloadWorker(onDownloadJobDone, args.bufferSize, args.urls,
                System.Threading.ThreadPriority.Lowest);
            _localPathRoot = args.localPathRoot;
            _assetPathTransformer = args.assetPathTransformer;
            _password = args.password;
            _streamingAssets = new StreamingAssetsLoader();
            _streamingAssets.LoadEmbeddedManifest(streamingAssets =>
            {
                if (args.useBaseManifest)
                {
                    Helpers.ReadSAManifest(_password, (manifest, fileEntry) =>
                    {
                        SetManifest(manifest, fileEntry);
                    });
                }
                else
                {
                    Helpers.GetManifest(_localPathRoot, _worker, args.manifestChecksum, args.manifestSize, args.manifestRSize,
                        _password, args.manifestChunkSize, (manifest, fileEntry) =>
                        {
                            SetManifest(manifest, fileEntry);
                        });
                }
            });
        }

        protected void _LoadBundle(IEnumerator e)
        {
            if (_closed)
            {
                return;
            }

            _bundleLoaders.AddLast(e);
            if (_bundleLoaders.Count == 1)
            {
                JobScheduler.DispatchCoroutine(_BundleLoader());
            }
        }

        protected void _LoadAsset(IEnumerator e)
        {
            if (_closed)
            {
                return;
            }

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

        private void SetManifest(Manifest manifest, ManifestEntry fileEntry)
        {
            _manifestFileEntry = fileEntry;
            _manifestObject = manifest;
            _assetPath2Bundle.Clear();
            _bundlesMap.Clear();
            foreach (var bundle in _manifestObject.bundles)
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
            
            try
            {
                ResourceManager.GetListener().OnSetManifest();
            }
            catch (Exception exception)
            {
                Debug.LogWarningFormat("OnSetManifest exception\n{0}", exception);
            }
        }

        // 验证当前清单是否最新
        public void ValidateManifest(IList<string> urls, int retry, Action<EValidationResult> callback)
        {
            Helpers.ReadRemoteFile(urls, Manifest.ChecksumFileName, content =>
            {
                if (!string.IsNullOrEmpty(content))
                {
                    var fileEntry = JsonUtility.FromJson<ManifestEntry>(content);
                    if (fileEntry != null)
                    {
                        var eq = Helpers.IsManifestEntryEquals(_manifestFileEntry, fileEntry);

                        callback(eq ? EValidationResult.Latest : EValidationResult.Update);
                        return true;
                    }
                }

                return --retry == 0;
            });
        }

        // 临时, 检查当前清单是否最新
        private IEnumerator _ValidateManifest(IList<string> urls, int retry, Action<EValidationResult> callback)
        {
            var urlIndex = 0;
            var url = "";
            var checksumFileName = Manifest.ChecksumFileName;

            while (true)
            {
                if (urlIndex < urls.Count)
                {
                    if (urls[urlIndex].EndsWith("/"))
                    {
                        url = urls[urlIndex] + checksumFileName;
                    }
                    else
                    {
                        url = urls[urlIndex] + "/" + checksumFileName;
                    }

                    ++urlIndex;
                    url += "?checksum=" + DateTime.Now.Ticks;
                }

                var uwr = UnityWebRequest.Get(url);
                yield return uwr.SendWebRequest();
                if (uwr.error == null)
                {
                    try
                    {
                        var handler = uwr.downloadHandler;
                        var fileEntry = JsonUtility.FromJson<ManifestEntry>(handler.text);
                        if (fileEntry != null)
                        {
                            var eq = Helpers.IsManifestEntryEquals(_manifestFileEntry, fileEntry);
                            callback(eq ? EValidationResult.Latest : EValidationResult.Update);
                            yield break;
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogErrorFormat(": {0}", exception);
                    }
                }

                if (--retry == 0)
                {
                    callback(EValidationResult.Failed);
                    yield break;
                }

                Debug.LogWarningFormat("retry after checksum validation failed {0}", url);
                yield return new WaitForSeconds(1f);
            }
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
            return _bundlesMap.TryGetValue(bundleName, out bundleInfo) && IsBundleAvailable(bundleInfo);
        }

        private bool LoadBundleFile(UBundle bundle)
        {
            try
            {
                var fullPath = Path.Combine(_localPathRoot, bundle.name);
                var fileStream = Helpers.GetBundleStream(fullPath, bundle.bundleInfo);
                if (fileStream != null)
                {
                    // Stream 生命周期转由 UAssetBundleBundle 管理
                    bundle.Load(Helpers.GetDecryptStream(fileStream, bundle.bundleInfo, _password, _manifestObject.chunkSize));
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogErrorFormat("LoadBundleFile({0}) exception\n{1}", bundle.name, exception);
            }

            return false;
        }

        protected void Unload(UBundle bundle)
        {
            _bundles.Remove(bundle.name);
            // Debug.LogFormat("bundle unloaded: {0}", bundle.name);
        }

        private void _AddDependencies(UBundle bundle, EAssetHints hints, string[] dependencies)
        {
            if (dependencies != null)
            {
                for (int i = 0, size = dependencies.Length; i < size; i++)
                {
                    var depBundleInfo = dependencies[i];
                    var depBundle = GetBundle(depBundleInfo, hints);
                    if (bundle.AddDependency(depBundle))
                    {
                        _AddDependencies(bundle, hints, depBundle.bundleInfo.dependencies);
                    }
                }
            }
        }

        public void Close()
        {
            if (!_closed)
            {
                _closed = true;
                // JobScheduler.DispatchCoroutine(_OnClosing());
                _OnClosing();
            }
        }

        private void _OnClosing()
        {
            // 终止所有任务
            _jobs.Clear();
            _worker.Abort();
            _idleWorker.Abort();

            // while (_assetLoaders.Count > 1)
            // {
            //     _assetLoaders.RemoveLast();
            // }
            //
            // while (_bundleLoaders.Count > 1)
            // {
            //     _bundleLoaders.RemoveLast();
            // }
            //
            // // 等待加载中的资源完成
            // while (_assetLoaders.Count == 1)
            // {
            //     yield return null;
            // }
            //
            // while (_bundleLoaders.Count == 1)
            // {
            //     yield return null;
            // }
            _assetLoaders.Clear();
            _bundleLoaders.Clear();

            var assetCount = _assets.Count;
            if (assetCount > 0)
            {
                var assets = new WeakReference[assetCount];
                _assets.Values.CopyTo(assets, 0);
                for (var i = 0; i < assetCount; i++)
                {
                    var weak = assets[i];
                    if (weak.IsAlive)
                    {
                        var asset = weak.Target as UAsset;
                        asset?.Dispose();
                    }
                }

                _assets.Clear();
            }

            var bundleCount = _bundles.Count;
            if (bundleCount > 0)
            {
                var bundles = new UBundle[bundleCount];
                _bundles.Values.CopyTo(bundles, 0);
                for (var i = 0; i < bundleCount; i++)
                {
                    var bundle = bundles[i];
                    // PrintLog($"关闭管理器, 强制释放资源包 {bundle.name}");
                    bundle.Release();
                }

                _bundles.Clear();
            }
        }

        public void CollectAssets(List<UAsset> assets)
        {
            foreach (var kv in _assets)
            {
                var weak = kv.Value;
                if (weak.IsAlive)
                {
                    var asset = weak.Target as UAsset;
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
            }
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
            return GetBundle(bundleName, EAssetHints.None);
        }

        public UBundle GetBundle(string bundleName, EAssetHints hints)
        {
            var bundleInfo = GetBundleInfo(bundleName);
            return GetBundle(bundleInfo, hints);
        }

        // 尝试获取包对象 (不会自动创建并加载)
        private UBundle TryGetBundle(Manifest.BundleInfo bundleInfo)
        {
            if (_closed)
            {
                return null;
            }

            UBundle bundle;
            return bundleInfo != null && _bundles.TryGetValue(bundleInfo.name, out bundle) ? bundle : null;
        }

        // 尝试获取包对象 (不会自动创建并加载)
        public UBundle TryGetBundle(string bundleName)
        {
            if (_closed)
            {
                return null;
            }

            UBundle bundle;
            return bundleName != null && _bundles.TryGetValue(bundleName, out bundle) ? bundle : null;
        }

        //TODO: hints
        private UBundle GetBundle(Manifest.BundleInfo bundleInfo, EAssetHints hints)
        {
            if (_closed)
            {
                return null;
            }

            UBundle bundle = null;
            if (bundleInfo != null)
            {
                var bundleName = bundleInfo.name;
                if (!_bundles.TryGetValue(bundleName, out bundle))
                {
                    switch (bundleInfo.type)
                    {
                        case Manifest.BundleType.AssetBundle:
                            bundle = new UAssetBundleBundle(this, bundleInfo, hints);
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
                        _AddDependencies(bundle, hints, bundle.bundleInfo.dependencies);
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
            return new UScene(GetAsset(assetPath, false, null, EAssetHints.None)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UScene(GetAsset(assetPath, false, null, EAssetHints.None)).LoadAdditive();
        }

        public IFileSystem GetFileSystem(string bundleName)
        {
            if (_closed)
            {
                return null;
            }

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

        public UAsset GetAsset(string assetPath, Type type, EAssetHints hints)
        {
            return GetAsset(assetPath, true, type, hints);
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
            if (TryGetBundleNameByAssetPath(transformedAssetPath, out bundleName))
            {
                var bundleInfo = GetBundleInfo(bundleName);
                if (bundleInfo != null)
                {
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
            if (_closed)
            {
                return null;
            }

            string bundleName;
            if (TryGetBundleNameByAssetPath(TransformAssetPath(assetPath), out bundleName))
            {
                return bundleName;
            }

            return null;
        }

        private bool TryGetBundleNameByAssetPath(string transformedAssetPath, out string bundleName)
        {
            return _assetPath2Bundle.TryGetValue(transformedAssetPath, out bundleName);
        }

        //TODO: hints
        private UAsset GetAsset(string assetPath, bool concrete, Type type, EAssetHints hints)
        {
            if (_closed)
            {
                return null;
            }

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
            if (TryGetBundleNameByAssetPath(transformedAssetPath, out bundleName))
            {
                var bundle = this.GetBundle(bundleName, hints);
                if (bundle != null)
                {
                    try
                    {
                        bundle.AddRef();
                        ResourceManager.GetAnalyzer()?.OnAssetOpen(assetPath);
                        asset = bundle.CreateAsset(assetPath, type, concrete, hints);
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

            var invalid = new UFailureAsset(assetPath, type);
            _assets[TransformAssetPath(assetPath)] = new WeakReference(invalid);
            return invalid;
        }
    }
}