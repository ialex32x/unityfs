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
        public string guid;
    }

    [Serializable]
    public class AssetListData : ScriptableObject, ISerializationCallbackReceiver
    {
        public float timeSeconds = 30f;
        private HashSet<string> _keys = new HashSet<string>();
        public List<AssetTimestamp> timestamps = new List<AssetTimestamp>();

        public void Begin()
        {
#if UNITY_EDITOR
            if (timestamps.Count != 0)
            {
                _keys.Clear();
                timestamps.Clear();
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void End()
        {
        }

        public bool Contains(string guid)
        {
            return _keys.Contains(guid);
        }

        public void AddObject(float time, string assetPath)
        {
#if UNITY_EDITOR
            if (time < timeSeconds)
            {
                
                var subAssetIndex = assetPath.IndexOf('@');
                if (subAssetIndex >= 0)
                {
                    assetPath = assetPath.Substring(0, subAssetIndex);
                }
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(assetGuid))
                {
                    _keys.Add(assetGuid);
                    timestamps.Add(new AssetTimestamp()
                    {
                        time = time,
                        guid = assetGuid,
                    });
                    EditorUtility.SetDirty(this);
                }
                else
                {
                    Debug.LogWarningFormat(assetPath);
                }
            }
#endif
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _keys.Clear();
            for (var i = 0; i < timestamps.Count; i++)
            {
                _keys.Add(timestamps[i].guid);
            }
        }
    }
}
