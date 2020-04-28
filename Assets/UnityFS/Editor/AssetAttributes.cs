using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 首包选择
    [Serializable]
    public enum AssetPacker
    {
        Auto = 0, // 根据资源运行分析列表自动确定是否进入 StreamingAssets 
        Always = 1, // 总是进入
        Never = 2, // 不进
    }

    // 显式指定资源的优先级 (将改变所在 bundleSlice 的优先级)
    [Serializable]
    public class AssetAttributes
    {
        // public string assetGuid;
        public int priority;
        public AssetPacker packer;
    }

    [Serializable]
    public class AssetAttributesMap : Dictionary<string, AssetAttributes>, ISerializationCallbackReceiver
    {
#pragma warning disable 0649
        [SerializeField] private List<string> _keys;
        [SerializeField] private List<AssetAttributes> _values;
#pragma warning restore 0649

        public void OnBeforeSerialize()
        {
            if (_keys == null)
            {
                _keys = new List<string>();
            }
            else
            {
                _keys.Clear();
            }

            if (_values == null)
            {
                _values = new List<AssetAttributes>();
            }
            else
            {
                _values.Clear();
            }

            foreach (var kv in this)
            {
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            if (_keys != null && _values != null)
            {
                var count = Math.Min(_keys.Count, _values.Count);
                for (var i = 0; i < count; i++)
                {
                    this[_keys[i]] = _values[i];
                }
            }
        }
    }

    [Serializable]
    public class SList 
    {
#pragma warning disable 0649
        [SerializeField] private List<string> _values;
#pragma warning restore 0649

        public int Count => _values.Count;

        public string this[int index]
        {
            get { return _values[index]; }
        }
        
        public SList(params string[] values)
        {
            _values = new List<string>();
            foreach (var value in values)
            {
                _values.Add(value);
            }
        }

        public bool Contains(string val)
        {
            return _values.Contains(val);
        }

        public void Add(string val)
        {
            if (!_values.Contains(val))
            {
                _values.Add(val);
            }
        }

        public bool Remove(string val)
        {
            return _values.Remove(val);
        }
    }
}