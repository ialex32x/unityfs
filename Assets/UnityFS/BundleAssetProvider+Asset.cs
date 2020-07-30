using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS
{
    using UnityEngine;

    public partial class BundleAssetProvider
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

        public class UFileListBundle : UBundle
        {
            private FileListManifest _fileListManifest;
            private BundleAssetProvider _provider;

            public FileListManifest manifest
            {
                get { return _fileListManifest; }
            }

            public UFileListBundle(BundleAssetProvider provider, Manifest.BundleInfo bundleInfo)
                : base(bundleInfo)
            {
                _provider = provider;
            }

            protected override void OnRelease()
            {
                base.OnRelease();
                _fileListManifest = null;
                _provider.Unload(this);
            }

            public override void Load(Stream stream)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_loaded)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var json = reader.ReadToEnd();
                        try
                        {
                            _fileListManifest = JsonUtility.FromJson<FileListManifest>(json);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogErrorFormat("FileListManifest parse failed: {0}\n{1}", json, exception);
                        }
                    }

                    _loaded = true;
                    // Debug.Log($"filelist loaded {name}");
                    if (_IsDependenciesLoaded())
                    {
                        OnLoaded();
                    }
                }
            }

            public override UAsset CreateAsset(string assetPath, Type type, bool concrete, EAssetHints hints)
            {
                if (_disposed)
                {
                    return null;
                }

                return new UFileListBundleAsset(this, assetPath);
            }
        }

        public class UFileListBundleAsset : UAsset
        {
            protected UFileListBundle _bundle;

            public FileListManifest manifest
            {
                get { return _bundle.manifest; }
            }

            public UFileListBundleAsset(UFileListBundle bundle, string assetPath)
                : base(assetPath, null)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    // Debug.LogFormat("UZipArchiveBundleAsset {0} released [{1}]", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() => // resurrecting 
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(_assetPath);
                        _bundle.completed -= OnBundleLoaded;
                        _bundle.RemoveRef();
                    });
                }
            }

            protected override bool IsAvailable()
            {
                throw new NotImplementedException();
            }

            public override byte[] ReadAllBytes()
            {
                return null;
            }

            public override object GetValue()
            {
                return _bundle.manifest;
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

            // (生命周期转由 UAssetBundleBundle 管理)
            public override void Load(Stream stream)
            {
                if (!_disposed && _zipFile == null && !_loaded)
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

            public override UAsset CreateAsset(string assetPath, Type type, bool concrete, EAssetHints hints)
            {
                if (_disposed)
                {
                    return null;
                }
                return new UZipArchiveBundleAsset(this, assetPath);
            }
        }

        // Zip 包中的文件资源
        protected class UZipArchiveBundleAsset : UAsset
        {
            protected UZipArchiveBundle _bundle;

            public UZipArchiveBundleAsset(UZipArchiveBundle bundle, string assetPath)
                : base(assetPath, null)
            {
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    // Debug.LogFormat("UZipArchiveBundleAsset {0} released [{1}]", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() => // resurrecting 
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(_assetPath);
                        _bundle.completed -= OnBundleLoaded;
                        _bundle.RemoveRef();
                    });
                }
            }

            protected override bool IsAvailable()
            {
                if (_disposed)
                {
                    return false;
                }
                return _bundle.Exists(_assetPath);
            }

            public override byte[] ReadAllBytes()
            {
                if (_disposed)
                {
                    return null;
                }
                return _bundle.ReadAllBytes(_assetPath);
            }

            public Stream OpenRead()
            {
                if (_disposed)
                {
                    return null;
                }
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
            private EAssetHints _hints;
            private Stream _stream; // manage the stream lifecycle (dispose after assetbundle.unload)
            private AssetBundle _assetBundle;
            private BundleAssetProvider _provider;

            public UAssetBundleBundle(BundleAssetProvider provider, Manifest.BundleInfo bundleInfo, EAssetHints hints)
                : base(bundleInfo)
            {
                _hints = hints;
                _provider = provider;
            }

            public bool IsAvailable()
            {
                if (_disposed)
                {
                    return false;
                }
                return _provider.IsBundleAvailable(_info);
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
                if (!_disposed && _stream == null && _provider != null)
                {
                    _stream = stream;
                    if ((_hints & EAssetHints.Synchronized) == 0)
                    {
                        _provider._LoadBundle(_LoadAsync());
                    }
                    else
                    {
                        _LoadSync();
                    }
                }
            }

            public void _LoadAsset(IEnumerator e)
            {
                if (_disposed)
                {
                    return;
                }
                _provider._LoadAsset(e);
            }

            private void _LoadSync()
            {
                if (_disposed)
                {
                    return;
                }
                var assetBundle = AssetBundle.LoadFromStream(_stream);
                OnAssetBundleLoaded(assetBundle);
            }

            private IEnumerator _LoadAsync()
            {
                AddRef();
                yield return null;
                if (!_disposed)
                {
                    var request = AssetBundle.LoadFromStreamAsync(_stream);
                    yield return request;
                    if (!_disposed)
                    {
                        OnAssetBundleLoaded(request.assetBundle);
                    }
                }
                RemoveRef();
            }

            public AssetBundle GetAssetBundle()
            {
                if (_disposed)
                {
                    return null;
                }
                return _assetBundle;
            }

            private void OnAssetBundleLoaded(AssetBundle assetBundle)
            {
                if (_disposed)
                {
                    return;
                }
                _assetBundle = assetBundle;
                _loaded = true;
                // Debug.Log($"assetbundle loaded {name}");
                if (_IsDependenciesLoaded())
                {
                    OnLoaded();
                }
            }

            public override UAsset CreateAsset(string assetPath, Type type, bool concrete, EAssetHints hints)
            {
                if (_disposed)
                {
                    return null;
                }

                if (concrete)
                {
                    return new UAssetBundleConcreteAsset(this, assetPath, type, hints);
                }

                return new UAssetBundleAsset(this, assetPath, type, hints);
            }
        }

        // 从 AssetBundle 资源包载入 (不实际调用 assetbundle.LoadAsset)
        protected class UAssetBundleAsset : UAsset
        {
            protected EAssetHints _hints;
            protected UAssetBundleBundle _bundle;

            public UAssetBundleAsset(UAssetBundleBundle bundle, string assetPath, Type type, EAssetHints hints)
                : base(assetPath, type)
            {
                _hints = hints;
                _bundle = bundle;
                _bundle.AddRef();
                _bundle.completed += OnBundleLoaded;
            }

            protected override bool IsAvailable()
            {
                return _bundle.IsAvailable();
            }

            public override byte[] ReadAllBytes()
            {
                if (_type != null && _type != typeof(TextAsset))
                {
                    throw new InvalidCastException(string.Format("{0} != TextAsset", _type));
                }
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
                    // Debug.LogFormat("UAssetBundleAsset {0} released [{1}] {2}", _assetPath, bManaged, _bundle.name);
                    _disposed = true;
                    JobScheduler.DispatchMain(() => // resurrecting 
                    {
                        ResourceManager.GetAnalyzer()?.OnAssetClose(_assetPath);
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
            private Object[] _objects = _emptyObjects;

            public UAssetBundleConcreteAsset(UAssetBundleBundle bundle, string assetPath, Type type, EAssetHints hints)
                : base(bundle, assetPath, type, hints)
            {
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
                    if ((_hints & EAssetHints.Synchronized) == 0)
                    {
                        _bundle._LoadAsset(_LoadAsync(assetBundle));
                    }
                    else
                    {
                        _LoadSync(assetBundle);
                    }
                }
                else
                {
                    Complete(); // failed
                }
            }

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
                        if (_type == null || _type == obj.GetType())
                        {
                            return obj;
                        }
                    }
                }
                return null;
            }

            private void _LoadSync(AssetBundle assetBundle)
            {
                if (_type != null && _type == typeof(Sprite))
                {
                    if (!_disposed)
                    {
                        _objects = assetBundle.LoadAssetWithSubAssets(_assetPath);
                        _object = _objects != null && _objects.Length > 0 ? _objects[0] : null;
                        Complete();
                    }
                }
                else
                {
                    if (!_disposed)
                    {
                        _object = _type != null
                            ? assetBundle.LoadAsset(_assetPath, _type)
                            : assetBundle.LoadAsset(_assetPath);
                        Complete();
                    }
                }
            }

            private IEnumerator _LoadAsync(AssetBundle assetBundle)
            {
                if (_type != null && _type == typeof(Sprite))
                {
                    var request = assetBundle.LoadAssetWithSubAssetsAsync(_assetPath);
                    yield return request;

                    if (!_disposed)
                    {
                        _objects = request.allAssets;
                        _object = _objects != null && _objects.Length > 0 ? _objects[0] : null;
                        Complete();
                    }
                }
                else
                {
                    var request = _type != null
                        ? assetBundle.LoadAssetAsync(_assetPath, _type)
                        : assetBundle.LoadAssetAsync(_assetPath);
                    yield return request;

                    if (!_disposed)
                    {
                        _object = request.asset;
                        Complete();
                    }
                }
            }
        }
    }
}