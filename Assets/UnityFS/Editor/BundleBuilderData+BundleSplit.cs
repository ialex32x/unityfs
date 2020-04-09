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
        public class BundleSplit
        {
            public bool encrypted;
            public string name = string.Empty; // 分包名
            public int sliceObjects;
            public List<BundleSplitRule> rules = new List<BundleSplitRule>();

            // scan 过程收集将要打入此 split 的所有资源的列表
            private List<Object> _assets = new List<Object>();

            public List<BundleSlice> slices = new List<BundleSlice>();

            public bool AddObject(Object asset)
            {
                _assets.Add(asset);
                return true;
            }

            public bool ContainsObject(Object asset)
            {
                return _assets.Contains(asset);
            }

            public void Cleanup()
            {
                _assets.Clear();
                foreach (var slice in slices)
                {
                    slice.Cleanup();
                }
            }

            public bool Slice(string bundleName)
            {
                var dirty = false;
                foreach (var asset in _assets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (AdjustBundleSlice(bundleName, guid))
                    {
                        dirty = true;
                    }
                }

                return dirty;
            }

            // 将 slice 切分命名插入 split 命名与文件后缀名之间
            public string GetBundleName(string bundleName)
            {
                var baseName = this.name ?? string.Empty;
                if (this.sliceObjects != 0 && this.slices.Count != 0)
                {
                    baseName = "_" + baseName + "_" + this.slices.Count;
                }

                if (string.IsNullOrEmpty(baseName))
                {
                    return bundleName;
                }

                var dot = bundleName.LastIndexOf('.');
                string prefix;
                string suffix;
                if (dot >= 0)
                {
                    prefix = bundleName.Substring(0, dot);
                    suffix = bundleName.Substring(dot);
                }
                else
                {
                    prefix = bundleName;
                    suffix = string.Empty;
                }

                return prefix + baseName + suffix;
            }

            public bool AdjustBundleSlice(string bundleName, string guid)
            {
                for (var i = 0; i < this.slices.Count; i++)
                {
                    var oldSlice = this.slices[i];
                    if (oldSlice.AddHistory(guid))
                    {
                        return false;
                    }
                }

                var count = this.slices.Count;
                var lastSlice = count > 0 ? this.slices[count - 1] : null;
                if (lastSlice == null || !lastSlice.AddNew(guid))
                {
                    var sliceName = GetBundleName(bundleName).ToLower();
                    var newSlice = new BundleSlice(sliceName, this.sliceObjects);
                    this.slices.Add(newSlice);
                    newSlice.AddNew(guid);
                }

                return true;
            }
        }
    }
}