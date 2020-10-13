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
        public class BundleSlice
        {
            public string name;
            public int capacity;
            public bool streamingAssets; // 是否进入 StreamingAssets
            public PackagePlatform platform; // 打包资源的平台性质
            public List<string> histroy = new List<string>();

            public long totalRawSize; // 原始资源大小统计 (不准确, 目前没统计依赖)
            public long lastBuildSize; // 最近一次打包的实际大小

            // 最终进入打包的所有资源对象
            public List<string> _assetGuids = new List<string>();

            public int GetAssetCount()
            {
                return _assetGuids.Count; 
            }

            public string GetAssetGuid(int index)
            {
                return _assetGuids[index];
            }

            public BundleSlice(string name, int capacity, bool streamingAssets, PackagePlatform platform)
            {
                this.name = name;
                this.capacity = capacity;
                this.streamingAssets = streamingAssets;
                this.platform = platform;
            }

            public void ForEachAsset(Action<string> visitor)
            {
                for (int i = 0, size = GetAssetCount(); i < size; i++)
                {
                    visitor(GetAssetGuid(i));
                }
            }

            public bool Lookup(string assetGuid)
            {
                return _assetGuids.Contains(assetGuid);
            }

            // 是否为指定平台打包
            public bool IsBuild(PackagePlatform buildPlatform)
            {
                return this.platform == PackagePlatform.Any || this.platform == buildPlatform;
            }

            // 完全重置此分包 (丢弃历史记录)
            public void Reset()
            {
                histroy.Clear();
                Cleanup();
            }

            public void Cleanup()
            {
                totalRawSize = 0;
                _assetGuids.Clear();
            }

            private void _AddAsset(string guid)
            {
                _assetGuids.Add(guid);
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var fileInfo = new FileInfo(assetPath);
                if (fileInfo.Exists)
                {
                    totalRawSize += fileInfo.Length;
                }
            }

            // 如果是历史资源, 将加入; 否则返回 false
            public bool AddHistory(string guid, bool streamingAssets, PackagePlatform platform)
            {
                if (histroy.Contains(guid))
                {
                    if (this.streamingAssets == streamingAssets && this.IsBuild(platform))
                    {
                        _AddAsset(guid);
                        return true;
                    }

                    // 此处的判定规则影响包的性质改变, 进而影响分包切分布局, 导致额外的包变更
                    if (GetAssetCount() == 0 && histroy.Count == 1)
                    {
                        this.streamingAssets = streamingAssets;
                        this.platform = platform;
                        _AddAsset(guid);
                        return true;
                    }
                }

                return false;
            }

            // 尝试添加资源
            // 仅历史数量在切分容量剩余时可以加入
            public bool AddNew(string guid)
            {
                if (capacity <= 0 || histroy.Count < capacity)
                {
                    _AddAsset(guid);
                    histroy.Add(guid);
                    return true;
                }

                return false;
            }
        }
    }
}