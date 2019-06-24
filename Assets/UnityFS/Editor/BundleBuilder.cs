using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilder
    {
        public const string BundleBuilderDataPath = "Assets/UnityFS/Data/default.asset";

        private static BundleBuilderData _data;

        public static BundleBuilderData GetData()
        {
            if (_data == null)
            {
                _data = AssetDatabase.LoadMainAssetAtPath(BundleBuilderDataPath) as BundleBuilderData;
                if (_data == null)
                {
                    _data = ScriptableObject.CreateInstance<BundleBuilderData>();
                    AssetDatabase.CreateAsset(_data, BundleBuilderDataPath);
                    AssetDatabase.SaveAssets();
                }
            }
            return _data;
        }
    }
}
