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
            public UAssetDatabaseAsset(string assetPath)
            : base(assetPath)
            {
#if UNITY_EDITOR
                // Application.LoadLevelAdditiveAsync()
                _object = UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath);
#endif
                Complete();
            }

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    Debug.LogFormat($"UAssetDatabaseAsset ({_assetPath}) released");
                    _disposed = true;
                }
            }
        }

        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();

        public UAsset GetAsset(string assetPath)
        {
            WeakReference assetRef;
            UAsset asset = null;
            if (_assets.TryGetValue(assetPath, out assetRef) && assetRef.IsAlive)
            {
                asset = assetRef.Target as UAsset;
                if (asset != null)
                {
                    return asset;
                }
            }
            asset = new UAssetDatabaseAsset(assetPath);
            _assets[assetPath] = new WeakReference(asset);
            return asset;
        }

        public UBundle GetBundle(string bundleName)
        {
            return null;
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
            return new UEditorScene(GetAsset(assetPath)).Load();
        }

        public UScene LoadSceneAdditive(string assetPath)
        {
            return new UEditorScene(GetAsset(assetPath)).LoadAdditive();
        }

        public void Close()
        {
        }
    }
}
