using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public abstract class UAsset
    {
        protected string _assetPath;
        protected bool _loaded;
        protected Object _object;

        // 注册前需要判断是否已经加载, 如果已经加载, 直接获取对象
        public Action Loaded;

        public bool isLoaded
        {
            get { return _loaded; }
        }

        public string assetPath
        {
            get { return _assetPath; }
        }

        public Object GetObject()
        {
            return _object;
        }

        public UAsset(string assetPath)
        {
            _assetPath = assetPath;
        }

        protected void OnLoaded()
        {
            _loaded = true;
            if (Loaded != null)
            {
                Loaded();
            }
        }
    }
}
