using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // Unity 资源抽象
    public abstract class UAsset : IDisposable
    {
        protected string _assetPath;
        protected Object _object;

        protected bool _disposed;
        protected bool _loaded;
        private List<Action<UAsset>> _callbacks = new List<Action<UAsset>>();

        public event Action<UAsset> completed
        {
            add
            {
                if (_disposed)
                {
                    Debug.LogError($"uasset already disposed ({_assetPath})");
                }
                if (_loaded)
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
            get { return _loaded; }
        }

        public string assetPath
        {
            get { return _assetPath; }
        }

        public Object GetObject()
        {
            if (_disposed)
            {
                Debug.LogError($"uasset already disposed ({_assetPath})");
            }
            return _object;
        }

        // 为 filesystem 提供兼容性接口 (每次调用返回一份拷贝数据)
        public abstract byte[] ReadAllBytes();

        public UAsset(string assetPath)
        {
            _assetPath = assetPath;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UAsset()
        {
            Dispose(false);
        }

        protected abstract void Dispose(bool bManaged);

        protected void OnLoaded()
        {
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks[0];
                _callbacks.RemoveAt(0);
                callback(this);
            }
        }

        protected void Complete()
        {
            if (!_loaded)
            {
                // Debug.Log($"asset loaded {_assetPath}");
                _loaded = true;
                OnLoaded();
            }
        }
    }
}
