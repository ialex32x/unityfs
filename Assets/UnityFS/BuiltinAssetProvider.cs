using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // read from Resources (无法验证版本)
    public class BuiltinAssetProvider : IAssetProvider
    {
        public string tag => null;
        
        public int build => 0;
        
        protected class UBuiltinAsset : UAsset
        {
            public UBuiltinAsset(string assetPath, Type type)
            : base(assetPath, type)
            {
                var resPath = assetPath;
                var prefix = "Assets/";
                if (resPath.StartsWith(prefix))
                {
                    resPath = resPath.Substring(prefix.Length);
                }
                var request = type != null ? Resources.LoadAsync(resPath, type) : Resources.LoadAsync(resPath);
                request.completed += OnResourceLoaded;
            }

            public override byte[] ReadAllBytes()
            {
                var path = _assetPath;
                if (!path.EndsWith(".bytes"))
                {
                    path += ".bytes";
                }
                var textAsset = Resources.Load<TextAsset>(path);
                if (textAsset != null)
                {
                    return textAsset.bytes;
                }
                return null;
                // throw new NotSupportedException();
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    Debug.LogFormat("UBuiltinAsset {0} released [{1}]", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() =>
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(_assetPath);
                    });
                }
            }

            private void OnResourceLoaded(AsyncOperation op)
            {
                if (_disposed)
                {
                    return;
                }
                var request = op as ResourceRequest;
                _object = request.asset;
                Complete();
            }
        }

        // protected class BuiltinSceneUAsset : UAsset
        // {
        // }

        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();

        public event Action completed
        {
            add
            {
                value();
            }

            remove
            {
            }
        }

        public UAsset GetAsset(string assetPath, Type type)
        {
            WeakReference assetRef;
            UAsset asset = null;
            if (_assets.TryGetValue(assetPath, out assetRef) && assetRef.IsAlive)
            {
                asset = assetRef.Target as UAsset;
                if (asset != null)
                {
                    ResourceManager.GetAnalyzer()?.OnAssetAccess(assetPath);
                    return asset;
                }
            }
            ResourceManager.GetAnalyzer()?.OnAssetOpen(assetPath);
            asset = new UBuiltinAsset(assetPath, type);
            _assets[assetPath] = new WeakReference(asset);
            return asset;
        }

        public UBundle GetBundle(string bundleName)
        {
            return null;
        }

        public string Find(string assetPath)
        {
            return "builtin";
        }

        public void ForEachTask(Action<ITask> callback)
        {
        }

        public bool IsBundleAvailable(string bundleName)
        {
            return true;
        }

        public bool IsAssetAvailable(string assetPath)
        {
            return File.Exists(assetPath);
        }

        public bool IsAssetExists(string assetPath)
        {
            return File.Exists(assetPath);
        }

        public IFileSystem GetFileSystem(string bundleName)
        {
            return new OrdinaryFileSystem(null);
        }

        public UScene LoadScene(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath, null)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath, null)).LoadAdditive();
        }

        public void Open(ResourceManagerArgs args)
        {
            try
            {
                ResourceManager.GetListener().OnSetManifest();
            }
            catch (Exception exception)
            {
                Debug.LogWarningFormat("OnSetManifest exception\n{0}", exception);
            }
        }

        public void Close()
        {
        }

        public IList<DownloadWorker.JobInfo> EnsureBundles(Manifest.BundleLoad load, Action onComplete)
        {
            onComplete?.Invoke();
            return new List<DownloadWorker.JobInfo>();
        }

        public DownloadWorker.JobInfo EnsureBundle(Manifest.BundleInfo bundleInfo)
        {
            return null;
        }

        public IList<Manifest.BundleInfo> GetInvalidatedBundles()
        {
            return new List<Manifest.BundleInfo>();
        }
    }
}
