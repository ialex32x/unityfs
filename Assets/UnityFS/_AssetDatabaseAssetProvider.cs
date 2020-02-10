using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 仅编辑器运行时可用
    public class AssetDatabaseAssetProvider : IAssetProvider
    {
        protected class UAssetDatabaseAsset : UAsset
        {
            public UAssetDatabaseAsset(string assetPath, Type type)
            : base(assetPath)
            {
#if UNITY_EDITOR
                // Application.LoadLevelAdditiveAsync()
                _object = type != null
                    ? UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, type)
                    : UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath);
#endif
                Complete();
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
                    Debug.LogFormat("UAssetDatabaseAsset {0} released {1}", _assetPath, bManaged);
                    _disposed = true;
                    JobScheduler.DispatchMain(() =>
                    {
                        ResourceManager.GetAnalyzer().OnAssetClose(assetPath);
                    });
                }
            }
        }

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
                    ResourceManager.GetAnalyzer().OnAssetAccess(assetPath);
                    return asset;
                }
            }
            ResourceManager.GetAnalyzer().OnAssetOpen(assetPath);
            if (File.Exists(assetPath))
            {
                asset = new UAssetDatabaseAsset(assetPath, type);
            }
            else
            {
                asset = new UFailureAsset(assetPath);
            }
            _assets[assetPath] = new WeakReference(asset);
            return asset;
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
            return File.Exists(assetPath);
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
            return new UEditorScene(GetAsset(assetPath, null)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath, null)).LoadAdditive();
        }

        public void Open()
        {
            ResourceManager.GetListener().OnSetManifest();
        }

        public void Close()
        {
        }
    }
}
