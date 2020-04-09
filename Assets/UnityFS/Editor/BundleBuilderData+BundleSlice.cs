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

            public List<string> histroy = new List<string>();

            public BundleSlice(string name, int cap)
            {
                this.name = name;
                this.capacity = cap;
            }

            public void Cleanup()
            {
                assetGuids.Clear();
            }

            // 如果是历史资源, 将加入; 否则返回 false
            public bool AddHistory(string guid)
            {
                if (histroy.Contains(guid))
                {
                    assetGuids.Add(guid);
                    return true;
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