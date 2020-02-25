using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    public class PrefabPool
    {
        public class Handle
        {
            public static readonly Handle Empty = null;

            private GameObject _gameObject;
            private PrefabPool _pool;
            private List<Action> _callbacks;

            public GameObject gameObject { get { return _gameObject; } }

            public bool isValid { get { return _gameObject != null; } }

            public bool isLoaded { get { return _pool.isLoaded; } }

            public string name { get { return _gameObject?.name; } set { if (_gameObject != null) _gameObject.name = value; } }

            public event Action completed
            {
                add
                {
                    if (_pool.isLoaded)
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

            public Transform transform
            {
                get { return _gameObject?.transform; }
            }

            public Transform parent
            {
                get { return transform?.parent; }
                set
                {
                    if (_gameObject != null)
                    {
                        _gameObject.transform.parent = value;
                    }
                }
            }

            public bool activeSelf
            {
                get { return _gameObject != null ? _gameObject.activeSelf : false; }
            }

            public bool activeInHierarchy
            {
                get { return _gameObject != null ? _gameObject.activeInHierarchy : false; }
            }

            public Handle(PrefabPool pool)
            {
                _pool = pool;
                _gameObject = null;
                _pool.completed += OnPoolCompleted;
            }

            public Handle(PrefabPool pool, GameObject gameObject)
            {
                _pool = pool;
                _gameObject = gameObject;
            }

            private void OnPoolCompleted()
            {
                _gameObject = _pool.Instantiate();
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
                            UnityEngine.Debug.LogErrorFormat("Handle({0}) Exception: {1}", _pool.assetPath, exception);
                        }
                    }
                }
            }

            public void SetParent(Transform parent, bool worldPositionStays = true)
            {
                if (_gameObject != null)
                {
                    _gameObject.transform.SetParent(parent, worldPositionStays);
                }
            }

            public void SetActive(bool bActive)
            {
                if (_gameObject != null)
                {
                    _gameObject.SetActive(bActive);
                }
            }

            public T GetComponent<T>()
            where T : Component
            {
                return _gameObject?.GetComponent<T>();
            }

            public Component GetComponent(Type type)
            {
                return _gameObject?.GetComponent(type);
            }

            public void Release()
            {
                if (_pool != null)
                {
                    var gameObject = _gameObject;
                    _gameObject = null;
                    _pool.completed -= OnPoolCompleted;
                    _pool.Destroy(gameObject);
                }
            }
        }
        private UAsset _asset;
        private int _count;
        private int _capacity;
        private List<GameObject> _gameObjects;
        private List<Action> _callbacks;
        private Transform _root;

        private Quaternion _localRotation;
        private Vector3 _localScale;

        // 实例化数量
        public int count { get { return _count; } }

        // 缓存数量
        public int poolSize { get { return _gameObjects != null ? _gameObjects.Count : 0; } }

        public int capacity
        {
            get { return _capacity; }
            set { _capacity = value; }
        }

        public bool isLoaded { get { return _asset.isLoaded; } }

        public string assetPath { get { return _asset.assetPath; } }

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
            var prefab = asset.GetObject() as GameObject;
            if (prefab != null)
            {
                _localRotation = prefab.transform.localRotation;
                _localScale = prefab.transform.localScale;
            }
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

        public Handle GetHandle()
        {
            return new Handle(this);
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
                var gameObject = _gameObjects[count - 1];
                _gameObjects.RemoveAt(count - 1);
                _count++;
                return gameObject;
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
#if UNITY_EDITOR
                if (_gameObjects != null && _gameObjects.Contains(gameObject))
                {
                    Debug.LogErrorFormat("重复销毁 GameObject: {0}", _asset.assetPath);
                    return;
                }
#endif
                gameObject.transform.SetParent(_root, false);
                gameObject.SetActive(false);
                gameObject.transform.localRotation = _localRotation;
                gameObject.transform.localScale = _localScale;
                if (_gameObjects == null)
                {
                    _gameObjects = new List<GameObject>();
                    _gameObjects.Add(gameObject);
                    --_count;
                }
                else
                {
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
