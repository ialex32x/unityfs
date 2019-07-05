using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    // 创建一个占位用 GameObject, 异步加载指定prefab资源, 并实例化挂载与此节点
    public class PrefabLoader : MonoBehaviour
    {
        private UAsset _asset;

        protected bool _loaded;
        private List<Action<PrefabLoader>> _callbacks = new List<Action<PrefabLoader>>();

        public event Action<PrefabLoader> completed
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

        protected void OnLoaded()
        {
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks[0];
                _callbacks.RemoveAt(0);
                callback(this);
            }
        }

        public static PrefabLoader Instantiate(string assetPath)
        {
            var gameObject = new GameObject("Prefab Loader");
            var loader = gameObject.AddComponent<PrefabLoader>();
            loader.Load(assetPath);
            return loader;
        }

        public PrefabLoader DestroyAfter(float seconds)
        {
            StartCoroutine(Helpers.DestroyAfter(gameObject, seconds));
            return this;
        }

        private void Load(string assetPath)
        {
            _asset = ResourceManager.LoadAsset(assetPath);
            _asset.completed += OnCompleted;
        }

        private void OnCompleted(UAsset asset)
        {
            Object.Instantiate(asset.GetObject(), transform);
            if (!_loaded)
            {
                // Debug.Log($"asset loaded {_assetPath}");
                _loaded = true;
                OnLoaded();
            }
        }

        void OnDestroy()
        {
            _asset.completed -= OnCompleted;
            _asset = null;
        }
    }
}
