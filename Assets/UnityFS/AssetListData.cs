using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [Serializable]
    public class AssetTimestamp
    {
        public float time;
        public string assetPath;
    }

    [Serializable]
    public class AssetListData
    {
        public float timeSeconds = 30f;
        public List<AssetTimestamp> timestamps = new List<AssetTimestamp>();

        // assetPath set
        private HashSet<string> _keys = new HashSet<string>();

        public static void WriteTo(string filePath, AssetListData listData)
        {
            try
            {
                if (listData == null)
                {
                    listData = new AssetListData();
                }
                listData.OnBeforeSerialize();
                var json = JsonUtility.ToJson(listData);
                File.WriteAllText(filePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        public static AssetListData ReadFrom(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var data = JsonUtility.FromJson<AssetListData>(json);
                    data.OnAfterDeserialize();
                    return data;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public bool Begin()
        {
#if UNITY_EDITOR
            if (timestamps.Count != 0)
            {
                _keys.Clear();
                timestamps.Clear();
                return true;
            }
#endif
            return false;
        }

        public void End()
        {
        }

        public bool Contains(string assetPath)
        {
            return _keys.Contains(assetPath);
        }

        public bool AddObject(float time, string assetPath)
        {
#if UNITY_EDITOR
            if (time < timeSeconds)
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    _keys.Add(assetPath);
                    timestamps.Add(new AssetTimestamp()
                    {
                        time = time,
                        assetPath = assetPath,
                    });
                    return true;
                }
            }
#endif
            return false;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _keys.Clear();
            for (var i = 0; i < timestamps.Count; i++)
            {
                _keys.Add(timestamps[i].assetPath);
            }
        }
    }
}
