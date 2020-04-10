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

            public bool Slice(BundleBuilderData data, BundleBuilderData.BundleInfo bundleInfo, string bundleName)
            {
                var dirty = false;
                foreach (var asset in _assets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (AdjustBundleSlice(data, bundleInfo, bundleName, guid))
                    {
                        dirty = true;
                    }
                }

                return dirty;
            }
            
            private const string encodingBase = "23456789abcdefghijklmnopqrstuvwxyzABCDEFGHJMNPQRTVWY";
            
            private static string encode(long rawValue)
            {
                var result = "";
                do
                {
                    var part = rawValue % encodingBase.Length;
                    result += encodingBase[(int) part];
                    rawValue = (rawValue - part) / encodingBase.Length;
                } while (rawValue > 0);
                return result;
            }

            // 将 slice 切分命名插入 split 命名与文件后缀名之间
            public string GetBundleSliceName(string bundleName)
            {
                string part1;
                string part2;
                var dotIndex = bundleName.LastIndexOf(".");
                if (dotIndex >= 0)
                {
                    part1 = bundleName.Substring(0, dotIndex);
                    part2 = bundleName.Substring(dotIndex);
                }
                else
                {
                    part1 = bundleName;
                    part2 = "";
                }
                if (!string.IsNullOrEmpty(name))
                {
                    if (part1.Length > 0)
                    {
                        part1 += "_" + name;
                    }
                    else
                    {
                        part1 = name;
                    }
                }

                if (part1.Length > 0)
                {
                    part1 += "_";
                }

                var p1 = encode(DateTime.Now.Ticks);
                var p2 = encode(Random.Range(0, int.MaxValue));
                return part1 + p1 + "_" + p2 + part2;
            }

            // 返回最后一个符合 StreamingAssets 性质的 slice 包
            private BundleSlice GetLastSlice(bool streamingAssets)
            {
                var count = this.slices.Count;
                for (var i = count - 1; i >= 0; i--)
                {
                    var slice = slices[i];
                    if (slice.streamingAssets == streamingAssets)
                    {
                        return slice;
                    }
                }

                return null;
            }

            // 将指定资源放入合适的分包中, 产生变化时返回 true
            private bool AdjustBundleSlice(BundleBuilderData data, BundleBuilderData.BundleInfo bundleInfo, string bundleName, string guid)
            {
                var streamingAssets = data.IsStreamingAssets(guid, bundleInfo);
                for (var i = 0; i < this.slices.Count; i++)
                {
                    var oldSlice = this.slices[i];
                    if (oldSlice.streamingAssets == streamingAssets && oldSlice.AddHistory(guid))
                    {
                        return false;
                    }
                }

                var lastSlice = GetLastSlice(streamingAssets);
                if (lastSlice == null || !lastSlice.AddNew(guid))
                {
                    var sliceName = GetBundleSliceName(bundleName).ToLower();
                    var newSlice = new BundleSlice(sliceName, sliceObjects, streamingAssets);
                    this.slices.Add(newSlice);
                    newSlice.AddNew(guid);
                }

                return true;
            }
        }
    }
}