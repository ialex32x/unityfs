using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public enum AssetState
    {
        None,
        Loading, 
        Loaded, 
    }

    public abstract class UAsset
    {
        protected string _assetPath;
        protected AssetState _state;
        protected Object _object;

        public Action Loaded;

        public string assetPath
        {
            get { return _assetPath; }
        }

        public UAsset(string assetPath)
        {
            _assetPath = assetPath;
        }

        // 异步加载
        public abstract bool Load();
    }
}
