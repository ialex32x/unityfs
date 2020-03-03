using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEditor;

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
            if (timestamps.Count != 0)
            {
                timestamps.Clear();
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        public void End()
        {
        }

        public void AddObject(float time, string assetPath)
        {
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
#if UNITY_EDITOR
                    EditorUtility.SetDirty(this);
#endif
                }
                else
                {
                    Debug.LogWarningFormat(assetPath);
                }
            }
        }
    }
}
