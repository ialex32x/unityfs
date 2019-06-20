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

        // 同步加载资源, 如果加载成功则返回 Object, 否则返回 null
        public abstract Object LoadSync();

        // 异步加载
        public abstract bool Load();
    }
}
