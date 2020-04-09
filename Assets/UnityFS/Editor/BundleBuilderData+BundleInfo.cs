using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public partial class BundleBuilderData 
    {
        [Serializable]
        public class BundleInfo
        {
            public int id;
            public int buildOrder = 1000;
            public string name; // bundle filename
            public string note;
            public Manifest.BundleType type;
            public Manifest.BundleLoad load;
            public bool enabled = true;
            public bool streamingAssets = false; // 是否复制到 StreamingAssets 目录
            public int priority;
            public List<BundleAssetTarget> targets = new List<BundleAssetTarget>(); // 打包目标 (可包含文件夹)
            public List<BundleSplit> splits = new List<BundleSplit>();

            public static string GetAssetGUID(Object asset)
            {
                var assetPath = AssetDatabase.GetAssetOrScenePath(asset);
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                return guid;
            }

            public bool Slice()
            {
                var dirty = false;
                foreach (var split in splits)
                {
                    if (split.Slice(name))
                    {
                        dirty = true;
                    }
                }

                return dirty;
            }

            public void Cleanup()
            {
                foreach (var split in splits)
                {
                    split.Cleanup();
                }
            }
        }
    }
}