using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 仅编辑器运行时可用
    public class AssetDatabaseAssetProvider : IAssetProvider
    {
        protected class EditorUAsset : UAsset
        {
            public EditorUAsset(string assetPath)
            : base(assetPath)
            {
#if UNITY_EDITOR
                _object = UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath);
#endif
                Complete();
            }

            ~EditorUAsset()
            {
                Debug.LogFormat($"UAsset(AssetDatabase:{_assetPath}) released");
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
            var invalid = new EditorUAsset(assetPath);
            _assets[assetPath] = new WeakReference(invalid);
            return invalid;
        }
        
        public IFileSystem GetFileSystem(string bundleName)
        {
            return new OrdinaryFileSystem(null);
        }
    }
}
