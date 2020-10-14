using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 仅编辑器运行时可用
    public class AssetDatabaseAssetProvider : IAssetProvider
    {
        // 伪装异步加载过程
        private float _asyncSimMin;
        private float _asyncSimMax;

        public string tag => null;

        public int build => 0;

        // 仅调试用, 模拟文件列表 (不带过滤)
        protected class UAssetDatabaseFileListAsset : UAsset
        {
            private FileListManifest _fileListManifest;

            public UAssetDatabaseFileListAsset(string assetPath)
            : base(assetPath, null)
            {
                _fileListManifest = new FileListManifest();
                WalkDirectory(assetPath);
                Complete();
            }

            private void WalkDirectory(string path)
            {
                var files = Directory.GetFiles(path);
                for (int i = 0, size = files.Length; i < size; i++)
                {
                    var file = files[i];
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }
                    var entry = new FileEntry();
                    var fileInfo = new FileInfo(file);
                    entry.name = file.Replace('\\', '/');
                    entry.size = (int)fileInfo.Length;
                    entry.checksum = string.Empty;
                    // Debug.LogFormat("walk: {0}", entry.name);
                    _fileListManifest.files.Add(entry);
                }
                var dirs = Directory.GetDirectories(path);
                for (int i = 0, size = dirs.Length; i < size; i++)
                {
                    var dir = dirs[i];
                    WalkDirectory(dir);
                }
            }

            protected override bool IsAvailable()
            {
                return true;
            }

            protected override bool IsValid()
            {
                return true;
            }

            public override byte[] ReadAllBytes()
            {
                return null;
            }

            public override object GetValue()
            {
                return _fileListManifest;
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    // Debug.LogFormat("UAssetDatabaseFileListAsset {0} released {1}", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() =>
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(assetPath);
                    });
                }
            }
        }

        protected class UAssetDatabaseAsset : UAsset
        {
            private Object[] _objects;

            public UAssetDatabaseAsset(string assetPath, Type type, EAssetHints hints, float delay)
            : base(assetPath, type)
            {
                if ((hints & EAssetHints.Synchronized) == 0 && delay > 0f)
                {
                    JobScheduler.DispatchMainAfter(() =>
                    {
                        _OnAssetLoaded();
                    }, delay);
                }
                else
                {
                    _OnAssetLoaded();
                }
            }

            private void _OnAssetLoaded()
            {
                if (_disposed)
                {
                    return;
                }
#if UNITY_EDITOR
                // Application.LoadLevelAdditiveAsync()
                if (_type == null)
                {
                    var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
                    if (importer != null && importer.textureType == UnityEditor.TextureImporterType.Sprite)
                    {
                        LoadAllAssetsAtPath(assetPath);
                        Complete();
                        return;
                    }
                }
                else if (_type == typeof(Sprite))
                {
                    LoadAllAssetsAtPath(assetPath);
                    Complete();
                    return;
                }

                _object = _type != null
                    ? UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, _type)
                    : UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath);
#endif
                Complete();
            }

#if UNITY_EDITOR
            private void LoadAllAssetsAtPath(string assetPath)
            {
                _objects = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                _object = _objects != null && _objects.Length > 0 ? _objects[0] : null;
                Debug.Log(_object);
            }
#endif

            public override Object[] GetObjects()
            {
                return _objects;
            }

            protected override Object GetObjectWithName(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return _object;
                }
                for (int i = 0, count = _objects.Length; i < count; i++)
                {
                    var obj = _objects[i];
                    
                    if (obj.name == name)
                    {
                        return obj;
                    }
                }
                return null;
            }

            protected override bool IsValid()
            {
                return _object != null;
            }

            public override byte[] ReadAllBytes()
            {
                return File.ReadAllBytes(_assetPath);
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    // Debug.LogFormat("UAssetDatabaseAsset {0} released {1}", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() =>
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(assetPath);
                    });
                }
            }
        }

        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();

        public event Action completed
        {
            add { value(); }
            remove { }
        }

        public AssetDatabaseAssetProvider(float asyncSimMin, float asyncSimMax)
        {
            _asyncSimMin = asyncSimMin;
            _asyncSimMax = asyncSimMax;
        }

        public UAsset GetAsset(string assetPath, Type type, EAssetHints hints)
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
            if (Directory.Exists(assetPath))
            {
                asset = new UAssetDatabaseFileListAsset(assetPath);
            }
            else if (IsFileExists(assetPath))
            {
                asset = new UAssetDatabaseAsset(assetPath, type, hints, Random.Range(_asyncSimMin, _asyncSimMax));
            }
            else
            {
                asset = new UFailureAsset(assetPath, type);
            }
            _assets[assetPath] = new WeakReference(asset);
            return asset;
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

        private bool IsFileExists(string assetPath)
        {
            return File.Exists(assetPath);
        }

        public UBundle GetBundle(string bundleName)
        {
            return null;
        }

        public bool IsBundleAvailable(string bundleName)
        {
            return true;
        }

        public bool IsAssetAvailable(string assetPath)
        {
            return IsFileExists(assetPath);
        }

        public bool IsAssetExists(string assetPath)
        {
            return IsFileExists(assetPath);
        }

        public string Find(string assetPath)
        {
            return "assetdatabase";
        }

        public void ForEachTask(Action<ITask> callback)
        {
        }

        public IFileSystem GetFileSystem(string bundleName)
        {
            return new OrdinaryFileSystem(null);
        }

        public UScene LoadScene(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath, null, EAssetHints.None)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath, null, EAssetHints.None)).LoadAdditive();
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

        public IList<DownloadWorker.JobInfo> EnsureBundles(IList<Manifest.BundleInfo> bundleInfos, Action onComplete)
        {
            onComplete?.Invoke();
            return new List<DownloadWorker.JobInfo>();
        }

        public DownloadWorker.JobInfo EnsureBundle(Manifest.BundleInfo bundleInfo)
        {
            return null;
        }

        public IList<Manifest.BundleInfo> GetInvalidatedBundles(Manifest.BundleLoad load)
        {
            return new List<Manifest.BundleInfo>();
        }
    }
}
