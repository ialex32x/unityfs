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

        public static PrefabLoader Instantiate(string assetPath)
        {
            var gameObject = new GameObject("Prefab Loader");
            var loader = gameObject.AddComponent<PrefabLoader>();
            loader.Load(assetPath);
            return loader;
        }

        public void DestroyAfter(float seconds)
        {
            StartCoroutine(Helpers.DestroyAfter(gameObject, seconds));
        }

        private void Load(string assetPath)
        {
            _asset = ResourceManager.LoadAsset(assetPath);
            _asset.completed += OnCompleted;
        }

        private void OnCompleted(UAsset asset)
        {
            Object.Instantiate(asset.GetObject(), transform);
        }

        void OnDestroy()
        {
            _asset.completed -= OnCompleted;
            _asset = null;
        }
    }
}
