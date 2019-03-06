using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public enum AssetState
    {
        None,
        Available,
        Loading,
        Loaded,
    }

    public interface IRefCount
    {
        void AddRef();
        void RemoveRef();
    }

    public class ResourceManager : MonoBehaviour
    {
        private static ResourceManager _rm;
        private static List<IAsset> _recycleAssets = new List<IAsset>();

        public float recycleInterval = 5f;

        private float _time;

        private ResourceManifest _manifest;

        void Awake()
        {
            if (_rm != null)
            {
                Object.Destroy(this);
                return;
            }
            _rm = this;
        }

        void Update()
        {
            var now = Time.realtimeSinceStartup;
            var delta = now - _time;
            if (delta > recycleInterval)
            {
                _time = now;
                lock (_recycleAssets)
                {
                    for (int i = 0, count = _recycleAssets.Count; i < count; i++)
                    {
                        _recycleAssets[i].RemoveRef();
                    }
                    _recycleAssets.Clear();
                }
            }
        }

        public static void Recycle(IAsset asset)
        {
            lock (_recycleAssets)
            {
                _recycleAssets.Add(asset);
            }
        }
    }

    public interface IAsset : IRefCount
    {
        AssetState state { get; }
    }

    // 适配器
    public class Asset : IDisposable
    {
        private IAsset _target;

        public Asset(IAsset target)
        {
            _target = target;
            _target.AddRef();
        }

        ~Asset()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool managed)
        {
            var target = _target;
            if (target != null)
            {
                _target = null;
                if (managed)
                {
                    target.RemoveRef();
                }
                else
                {
                    ResourceManager.Recycle(target);
                }
            }
        }
    }

    public class BundleManifest
    {
        public string name;
        public int size;
        public string hash;
        public List<string> assets;

        public string GetAsset(string filename)
        {
            for (int j = 0, jsize = assets.Count; j < jsize; j++)
            {
                var asset = assets[j];
                if (asset == filename)
                {
                    return asset;
                }
            }
            return null;
        }
    }

    public class ResourceManifest
    {
        public List<BundleManifest> packages;
        public List<BundleManifest> zips;
    }

    public class FileEntry
    {
        public string name;     // 引用名
        public string filename; // 存盘名
        public string hash;
        public int size;
    }

    // 本地文件信息
    public class FileRegistry
    {
        public int size;
        public string hash;
        public ResourceManifest manifest;
        public List<FileEntry> entries;
    }

    public interface IFileSystem
    {
        bool Contains(string filename);     // 是否包含资源
        bool IsAvailable(string filename);  // 是否可以立即加载到资源
        Asset LoadAsset(string filename);
    }

    public abstract class AbstractFileSystem : IFileSystem
    {
        protected IAssetLoader _loader;

        public AbstractFileSystem(IAssetLoader loader)
        {
            _loader = loader;
        }

        public abstract bool Contains(string filename);
        public abstract bool IsAvailable(string filename);
        public abstract Asset LoadAsset(string filename);
    }

    public class PackageFileSystem : AbstractFileSystem
    {
        private List<BundleManifest> _manifests = new List<BundleManifest>();
        // asset => bundle lookup table
        private Dictionary<string, BundleManifest> _lookup = new Dictionary<string, BundleManifest>();
        private Dictionary<string, UnityAsset> _assets = new Dictionary<string, UnityAsset>();
        private Dictionary<string, BundleAsset> _bundles = new Dictionary<string, BundleAsset>();

        public PackageFileSystem(IAssetLoader loader, List<BundleManifest> manifests)
        : base(loader)
        {
            _manifests = manifests;
            for (int i = 0, isize = _manifests.Count; i < isize; i++)
            {
                var manifest = _manifests[i];
                foreach (var asset in manifest.assets)
                {
                    _lookup[asset] = manifest;
                }
            }
        }

        public override bool Contains(string filename)
        {
            return _lookup.ContainsKey(filename);
        }

        public override bool IsAvailable(string filename)
        {
            UnityAsset uasset;
            if (_assets.TryGetValue(filename, out uasset))
            {
                return uasset.state == AssetState.Loaded;
            }
            for (int i = 0, isize = _manifests.Count; i < isize; i++)
            {
                var manifest = _manifests[i];
                var asset = manifest.GetAsset(filename);
                if (asset != null)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class ZipFileSystem : AbstractFileSystem
    {
        private List<BundleManifest> _manifests = new List<BundleManifest>();
        private Dictionary<string, ZipEntryAsset> _assets = new Dictionary<string, ZipEntryAsset>();

        public ZipFileSystem(IAssetLoader loader)
        : base(loader)
        { }

        public override bool IsAvailable(string filename)
        {
            ZipEntryAsset uasset;
            if (_assets.TryGetValue(filename, out uasset))
            {
                return sync ? uasset.state == AssetState.Loaded : true;
            }
            if (!sync)
            {
                for (int i = 0, isize = _manifests.Count; i < isize; i++)
                {
                    var manifest = _manifests[i];
                    var asset = manifest.GetAsset(filename);
                    if (asset != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class DirectFileSystem : AbstractFileSystem
    {
        private Dictionary<string, FileAsset> _assets = new Dictionary<string, FileAsset>();
        private FileRegistry _registry;

        public DirectFileSystem(IAssetLoader loader, FileRegistry registry)
        : base(loader)
        {
            _registry = registry;
        }

        public override bool IsAvailable(string filename)
        {
            FileAsset uasset;
            if (_assets.TryGetValue(filename, out uasset))
            {
                return sync ? uasset.state == AssetState.Loaded : uasset.state;
            }
            return _registry.Search(filename);
        }
    }

    public class AssetDatabaseFileSystem : IFileSystem
    { }

    // public interface IPackage
    // { }

    // public class UnityBundle : IPackage
    // { }

    // 对应 AssetBundle
    public class BundleAsset : IAsset
    {
        private Stream _stream;
        private AssetBundle _ab;
    }

    // 对应 Object
    public class UnityAsset : IAsset
    {
        private int _id;
    }

    public class FileAsset : IAsset
    { }

    public class ZipEntryAsset : IAsset
    { }

    public interface IAssetRequest
    { }

    public interface IAssetLoader
    {
        // IAssetRequest Load(string filename);
    }

    // public class ResourcesAssetLoader : IAssetLoader
    // { }

    public class OSFileAssetLoader : IAssetLoader
    { }

    // public class StreamingAssetsLoader : IAssetLoader
    // { }

    public class RemoteAssetLoader : IAssetLoader
    {
        private IAssetWriter _writer;
    }

    public class ChainedAssetLoader : IAssetLoader
    { }

    public interface IAssetWriteOperation
    {
        // void Write(byte[] data);
        // void Close();
    }

    public interface IAssetWriter
    {
        IAssetWriteOperation Save(string filename);
    }
}