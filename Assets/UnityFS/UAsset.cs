using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // Unity 资源抽象
    public abstract class UAsset
    {
        protected string _assetPath;
        protected Object _object;

        protected bool _loaded;
        private List<Action<UAsset>> _callbacks = new List<Action<UAsset>>();

        public event Action<UAsset> completed
        {
            add
            {
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
            return _object;
        }

        public GameObject Instantiate()
        {
            var prefab = _object as GameObject;
            var go = Object.Instantiate<GameObject>(prefab);
            Utils.AssetHandle.Attach(go, this);
            return go;
        }

        public UAsset(string assetPath)
        {
            _assetPath = assetPath;
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

        protected void Complete()
        {
            _loaded = true;
            OnLoaded();
        }
    }
}
