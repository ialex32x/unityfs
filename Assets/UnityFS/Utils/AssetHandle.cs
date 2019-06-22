using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Utils
{
    using UnityEngine;

    // 将一个 UAsset 的强引用与 GameObject 关联, 影响 UAsset 的生命周期
    public class AssetHandle : MonoBehaviour
    {
        private UAsset _asset;

        public static AssetHandle Attach(GameObject gameObject, UAsset asset)
        {
            return Attach(gameObject, asset, 0.0f);
        }

        public static AssetHandle Attach(GameObject gameObject, UAsset asset, float ttl)
        {
            var handle = gameObject.AddComponent<AssetHandle>();
            handle._asset = asset;
            if (ttl > 0.0f)
            {
                handle.StartCoroutine(Helpers.DestroyAfter(gameObject, ttl));
            }
            return handle;
        }

        void OnDestroy()
        {
            _asset = null;
        }
    }
}
