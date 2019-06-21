using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 资源包资源管理
    public class BundleAssetProvider : IAssetProvider
    {
        protected class UBundle
        {
            public Action Loaded;

            private int _refCount;
            private bool _loaded;
            private AssetBundle _assetBundle;
            private BundleAssetProvider _provider;
            private Manifest.BundleInfo _info;

            public bool isLoaded
            {
                get { return _loaded; }
            }

            public string name
            {
                get { return _info.name; }
            }

            public UBundle(BundleAssetProvider provider, Manifest.BundleInfo info)
            {
                _provider = provider;
                _info = info;
            }

            public string GetAssetName(string assetPath)
            {
                return _info.assets[assetPath];
            }

            // main thread only
            public void AddRef()
            {
                _refCount++;
            }

            // main thread only
            public void RemoveRef()
            {
                _refCount--;
                if (_refCount == 0)
                {
                    this.Release();
                }
            }

            private void Release()
            {
                _assetBundle.Unload(true);
                _provider.Unload(this);
            }

            public void Load(Stream stream)
            {
                if (_refCount == 0)
                {
                    Debug.LogWarning("UBundle Load after released!!");
                    return;
                }
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
                if (Loaded != null)
                {
                    Loaded();
                }
            }
        }

        protected class BundleUAsset : UAsset
        {
            private UBundle _bundle;

            public BundleUAsset(UBundle bundle, string assetPath)
            : base(assetPath)
            {
                _bundle = bundle;
                if (_bundle != null)
                {
                    _bundle.AddRef();
                    if (_bundle.isLoaded)
                    {
                        OnBundleLoaded();
                    }
                    else
                    {
                        _bundle.Loaded += OnBundleLoaded;
                    }
                }
                else
                {
                    OnLoaded();
                }
            }

            ~BundleUAsset()
            {
                if (_bundle != null)
                {
                    JobScheduler.DispatchMain(() =>
                    {
                        _bundle.RemoveRef();
                    });
                }
            }

            private void OnBundleLoaded()
            {
                var assetBundle = _bundle.GetAssetBundle();
                if (assetBundle != null)
                {
                    var assetName = _bundle.GetAssetName(_assetPath);
                    var request = assetBundle.LoadAssetAsync(assetName);
                    request.completed += OnAssetLoaded;
                }
                else
                {
                    OnLoaded(); // failed
                }
            }

            private void OnAssetLoaded(AsyncOperation op)
            {
                var request = op as AssetBundleRequest;
                _object = request.asset;
                OnLoaded();
            }
        }

        // 资源路径 => 资源包 的快速映射
        private Dictionary<string, string> _assetPath2Bundle = new Dictionary<string, string>();
        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();
        private Dictionary<string, UBundle> _bundles = new Dictionary<string, UBundle>();
        private Manifest _manifest;
        private IFileProvider _provider;
        private IDownloader _downloader;

        public BundleAssetProvider(Manifest manifest, IFileProvider provider, IDownloader downloader)
        {
            _manifest = manifest;
            _provider = provider;
            _downloader = downloader;
            this.Initialize();
        }

        private void Initialize()
        {
            foreach (var bundle in _manifest.bundles)
            {
                foreach (var asset in bundle.Value.assets)
                {
                    _assetPath2Bundle[asset.Key] = bundle.Key;
                }
            }
        }

        protected void Unload(UBundle bundle)
        {
            _bundles.Remove(bundle.name);
        }

        protected UBundle GetBundle(string name)
        {
            UBundle bundle;
            if (!_bundles.TryGetValue(name, out bundle))
            {
                bundle = new UBundle(this, _manifest.bundles[name]);
                var fs = _provider.OpenFile(name);
                if (fs != null)
                {
                    bundle.Load(fs);
                }
                else
                {
                    var fileInfo = _manifest.files[name];
                    var task = new DownloadTask(fileInfo, -1, self =>
                    {
                        bundle.Load(self.OpenFile());
                    });
                    _downloader.AddDownloadTask(task);
                }
            }
            return bundle;
        }

        public UAsset GetAsset(string assetPath)
        {
            WeakReference assetRef;
            if (_assets.TryGetValue(assetPath, out assetRef) && assetRef.IsAlive)
            {
                var asset = assetRef.Target as UAsset;
                if (asset != null)
                {
                    return asset;
                }
            }
            string bundleName;
            if (_assetPath2Bundle.TryGetValue(assetPath, out bundleName))
            {
                var bundle = this.GetBundle(bundleName);
                var asset = new BundleUAsset(bundle, assetPath);
                _assets[assetPath] = new WeakReference(asset);
                return asset;
            }
            var invalid = new BundleUAsset(null, assetPath);
            _assets[assetPath] = new WeakReference(invalid);
            return invalid;
        }
    }
}
