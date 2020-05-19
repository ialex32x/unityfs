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

            // 最终进入打包的所有资源对象
            private List<string> _assetGuids;

            public List<string> assetGuids
            {
                get
                {
                    if (_assetGuids == null) _assetGuids = new List<string>();
                    return _assetGuids;
                }
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
                for (int i = 0, size = assetGuids.Count; i < size; i++)
                {
                    visitor(assetGuids[i]);
                }
            }

            public bool Lookup(string assetGuid)
            {
                return assetGuids.Contains(assetGuid);
            }

            // 是否为指定平台打包
            public bool IsBuild(PackagePlatform buildPlatform)
            {
                return this.platform == PackagePlatform.Any || this.platform == buildPlatform;
            }

            public void Reset()
            {
                histroy.Clear();
                assetGuids.Clear();
                Cleanup();
            }

            public void Cleanup()
            {
                assetGuids.Clear();
            }

            // 如果是历史资源, 将加入; 否则返回 false
            public bool AddHistory(string guid, bool streamingAssets, PackagePlatform platform)
            {
                if (histroy.Contains(guid))
                {
                    if (this.streamingAssets == streamingAssets && this.platform == platform)
                    {
                        assetGuids.Add(guid);
                        return true;
                    }

                    // 此处的判定规则影响包的性质改变, 进而影响分包切分布局, 导致额外的包变更
                    if (assetGuids.Count == 0 && histroy.Count == 1)
                    {
                        this.streamingAssets = streamingAssets;
                        this.platform = platform;
                        assetGuids.Add(guid);
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
                    assetGuids.Add(guid);
                    histroy.Add(guid);
                    return true;
                }

                return false;
            }
        }
    }
}