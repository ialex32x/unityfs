using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class UAssets
    {
        private List<UAsset> _assets = new List<UAsset>();
        private int _loaded;
        private List<Action<UAssets>> _callbacks = new List<Action<UAssets>>();

        public event Action<UAssets> completed
        {
            add
            {
                if (_loaded == _assets.Count)
                {
                    value(this);
                }
                else
                {
                    _callbacks.Add(value);
                }
            }
            remove
            {
                _callbacks.Remove(value);
            }
        }

        public bool isLoaded
        {
            get { return _loaded == _assets.Count; }
        }

        public UAssets()
        {
        }

        public UAssets AddRange(IList<string> assetPaths)
        {
            var assets = new List<UAsset>(assetPaths.Count);
            for (int i = 0, size = assetPaths.Count; i < size; i++)
            {
                var assetPath = assetPaths[i];
                var asset = ResourceManager.LoadAsset(assetPath);
                assets.Add(asset);
                _assets.Add(asset);
            }
            for (int i = 0, size = assets.Count; i < size; i++)
            {
                var asset = assets[i];
                asset.completed += OnAssetLoaded;
            }
            return this;
        }

        private void OnAssetLoaded(UAsset asset)
        {
            _loaded++;
            if (_loaded == _assets.Count)
            {
                OnLoaded();
            }
        }

        protected void OnLoaded()
        {
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks[0];
                _callbacks.RemoveAt(0);
                callback(this);
            }
        }
    }
}
