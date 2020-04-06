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

    public class AssetListData : ScriptableObject
    {
        public float timeSeconds = 30f;
        public List<AssetTimestamp> timestamps = new List<AssetTimestamp>();

        public void Begin()
        {
#if UNITY_EDITOR
            if (timestamps.Count != 0)
            {
                timestamps.Clear();
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void End()
        {
        }

        public void AddObject(float time, string assetPath)
        {
#if UNITY_EDITOR
            if (time < timeSeconds)
            {
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(assetGuid))
                {
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
    }
}
