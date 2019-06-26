using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // read from Resources (无法验证版本)
    public class BuiltinAssetProvider : IAssetProvider
    {
        protected class BuiltinUAsset : UAsset
        {
            public BuiltinUAsset(string assetPath)
            : base(assetPath)
            {
                var resPath = assetPath;
                var prefix = "Assets/";
                if (resPath.StartsWith(prefix))
                {
                    resPath = resPath.Substring(prefix.Length);
                }
                var request = Resources.LoadAsync(resPath);
                request.completed += OnResourceLoaded;
            }

            private void OnResourceLoaded(AsyncOperation op)
            {
                var request = op as ResourceRequest;
                _object = request.asset;
                Complete();
            }
        }

        private Dictionary<string, WeakReference> _assets = new Dictionary<string, WeakReference>();

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
            var invalid = new BuiltinUAsset(assetPath);
            _assets[assetPath] = new WeakReference(invalid);
            return invalid;
        }
        
        public IFileSystem GetFileSystem(string bundleName)
        {
            return new OrdinaryFileSystem(null);
        }
    }
}
