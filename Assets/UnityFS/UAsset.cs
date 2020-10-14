using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // Unity 资源抽象
    public abstract class UAsset : IDisposable
    {
        protected static Object[] _emptyObjects = new Object[0];
        protected string _assetPath;
        protected Type _type;
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
                    Debug.LogError($"event.completed: uasset already disposed ({_assetPath})");
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

        /// <summary>
        /// 此资源句柄是否已经销毁
        /// </summary>
        public bool isAlive { get { return !_disposed; } }

        /// <summary>
        /// 是否是有效资源 (不存在的资源返回 false)
        /// </summary>
        public bool isValid { get { return IsValid(); } }

        /// <summary>
        /// 是否已经加载完成
        /// </summary>
        public bool isLoaded { get { return _loaded; } }

        /// <summary>
        /// 是否本地直接可用 (即使直接可用, 加载过程仍然可能且应该认为是异步的)
        /// </summary>
        public bool isAvailable { get { return IsAvailable(); } }

        public string assetPath { get { return _assetPath; } }

        public Object GetObject()
        {
            if (_disposed)
            {
                Debug.LogError($"GetObject(): uasset already disposed ({_assetPath})");
                return null;
            }
            return GetObjectWithName(null);
        }

        public Object GetObject(string name)
        {
            if (_disposed)
            {
                Debug.LogError($"GetObject(): uasset already disposed ({_assetPath})");
                return null;
            }
            return GetObjectWithName(name);
        }

        public T GetObject<T>()
            where T : Object
        {
            if (_type != null && typeof(T) != _type)
            {
                throw new InvalidCastException(string.Format("{0} != {1}: {2}", _type, typeof(T), _assetPath));
            }
            return GetObject() as T;
        }

        public T GetObject<T>(string name)
            where T : Object
        {
            if (_type != null && typeof(T) != _type)
            {
                throw new InvalidCastException(string.Format("{0} != {1}: {2}", _type, typeof(T), _assetPath));
            }
            return GetObject(name) as T;
        }

        protected virtual Object GetObjectWithName(string name)
        {
            return _object;
        }

        public virtual Object[] GetObjects()
        {
            return _emptyObjects;
        }

        protected virtual bool IsValid()
        {
            return true;
        }

        protected virtual bool IsAvailable()
        {
            return false;
        }

        // 为 filesystem 提供兼容性接口 (每次调用返回一份拷贝数据)
        public abstract byte[] ReadAllBytes();

        public virtual object GetValue() { return null; }

        public UAsset(string assetPath, Type type)
        {
            _assetPath = assetPath;
            _type = type;
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
                try
                {
                    callback(this);
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("UAsset Exception: {0}\n{1}", _assetPath, exception);
                }
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
