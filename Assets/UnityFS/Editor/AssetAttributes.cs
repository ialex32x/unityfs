using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 显式指定资源的优先级 (将改变所在 bundleSlice 的优先级)
    [Serializable]
    public class AssetAttributes
    {
        // public string assetGuid;
        public int priority;
    }
 
    [Serializable]
    public class AssetAttributesMap: Dictionary<string, AssetAttributes>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<string> _keys;
        [SerializeField] private List<AssetAttributes> _values;

        public void OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();
            foreach (var kv in this)
            {
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            var count = Math.Min(_keys.Count, _values.Count);
            for (var i = 0; i < count; i++)
            {
                this[_keys[i]] = _values[i];
            }
        }
    }
}