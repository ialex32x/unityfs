using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // read from Resources (无法验证版本)
    public class BuiltinAssetProvider : IAssetProvider
    {
        protected class UBuiltinAsset : UAsset
        {
            public UBuiltinAsset(string assetPath, Type type)
            : base(assetPath)
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

            protected override void Dispose(bool bManaged)
            {
                if (!_disposed)
                {
                    Debug.LogFormat($"UBuiltinAsset ({assetPath}) released");
                    JobScheduler.DispatchMain(() =>
                    {
                        ResourceManager.GetAnalyzer().OnAssetClose(assetPath);
                    });
                    _disposed = true;
                }
            }

            private void OnResourceLoaded(AsyncOperation op)
            {
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
                    ResourceManager.GetAnalyzer().OnAssetAccess(assetPath);
                    return asset;
                }
            }
            ResourceManager.GetAnalyzer().OnAssetOpen(assetPath);
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
            ResourceManager.GetListener().OnComplete();
        }

        public void Close()
        {
        }
    }
}
