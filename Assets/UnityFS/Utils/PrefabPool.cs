using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public class PrefabPool
    {
        private UAsset _asset;
        private int _count;
        private int _capacity;
        private List<GameObject> _gameObjects;
        private List<Action> _callbacks;
        private Transform _root;

        // 实例化数量
        public int count { get { return _count; } }

        // 缓存数量
        public int poolSize { get { return _gameObjects != null ? _gameObjects.Count : 0; } }

        public int capacity
        {
            get { return _capacity; }
            set { _capacity = value; }
        }

        public event Action completed
        {
            add
            {
                if (_asset.isLoaded)
                {
                    value();
                }
                else
                {
                    if (_callbacks == null)
                    {
                        _callbacks = new List<Action>();
                    }
                    _callbacks.Add(value);
                }
            }

            remove
            {
                if (_callbacks != null)
                {
                    _callbacks.Remove(value);
                }
            }
        }

        public PrefabPool(Transform root, string assetPath, int capacity)
        {
            _root = root;
            _capacity = capacity;
            _asset = ResourceManager.LoadAsset(assetPath, typeof(GameObject));
            _asset.completed += onAssetLoaded;
        }

        private void onAssetLoaded(UAsset asset)
        {
            if (_callbacks == null)
            {
                return;
            }
            var shadows = _callbacks;
            var count = shadows.Count;
            if (count > 0)
            {
                _callbacks = null;
                for (var i = 0; i < count; i++)
                {
                    var cb = shadows[i];
                    try
                    {
                        cb();
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogErrorFormat("GameObjectPool({0}) Exception: {1}", _asset.assetPath, exception);
                    }
                }
            }
        }

        public GameObject Instantiate()
        {
            if (!_asset.isLoaded)
            {
                UnityEngine.Debug.LogErrorFormat("GameObjectPool({0}) 加载未完成", _asset.assetPath);
                return null;
            }
            var count = _gameObjects != null ? _gameObjects.Count : 0;
            if (count > 0)
            {
                var old = _gameObjects[count - 1];
                _gameObjects.RemoveAt(count - 1);
                _count++;
                return old;
            }
            var prefab = _asset.GetObject() as GameObject;
            if (prefab != null)
            {
                var gameObject = UnityEngine.Object.Instantiate(prefab);
                _count++;
                return gameObject;
            }
            return null;
        }

        public void Destroy(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }
            var poolSize = _gameObjects != null ? _gameObjects.Count : 0;
            if (_capacity > 0 && poolSize > _capacity)
            {
                --_count;
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                gameObject.transform.SetParent(_root);
                gameObject.SetActive(false);
                if (_gameObjects == null)
                {
                    _gameObjects = new List<GameObject>();
                    _gameObjects.Add(gameObject);
                    --_count;
                }
                else
                {
#if UNITY_EDITOR
                    if (_gameObjects.Contains(gameObject))
                    {
                        Debug.LogErrorFormat("重复销毁 GameObject: {0}", _asset.assetPath);
                        return;
                    }
#endif
                    _gameObjects.Add(gameObject);
                    --_count;
                }
            }
        }

        public void Drain()
        {
            if (_gameObjects == null)
            {
                return;
            }
            var shadow = _gameObjects;
            _gameObjects = null;
            var count = shadow.Count;
            for (var i = 0; i < count; i++)
            {
                var go = shadow[i];
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            shadow.Clear();
        }
    }
}
