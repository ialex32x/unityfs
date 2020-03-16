using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public class PackageBuildEntry
    {
        public PackageBuildInfo buildInfo;
        public string name; // 包名
        private List<PackageBuildEntry> _dependencies = new List<PackageBuildEntry>();
        public List<string> assetPaths = new List<string>(); // 直接资源
        public bool extracted; // 是否已经展开所有依赖
        public HashSet<string> extractedAssetPaths = new HashSet<string>(); // 展开所有依赖
        private List<PackageBuildEntry> _extractedDependencies = new List<PackageBuildEntry>();

        public void AddDependency(PackageBuildEntry entry)
        {
            if (entry != this && !_dependencies.Contains(entry))
            {
                _dependencies.Add(entry);
            }
        }

        private bool IsDependencies(string assetPath)
        {
            for (var i = 0; i < _dependencies.Count; i++)
            {
                if (_dependencies[i].extractedAssetPaths.Contains(assetPath))
                {
                    return true;
                }
            }

            return false;
        }

        public void Extract(List<PackageBuildEntry> pendingList, Dictionary<string, int> dict)
        {
            if (!pendingList.Contains(this))
            {
                pendingList.Add(this);
                for (var i = 0; i < _dependencies.Count; i++)
                {
                    _dependencies[i].Extract(pendingList, dict);
                }

                for (var i = 0; i < assetPaths.Count; i++)
                {
                    var assetPath = assetPaths[i];
                    if (extractedAssetPaths.Add(assetPath))
                    {
                        //TODO: add to dict count
                        var deps = AssetDatabase.GetDependencies(assetPath);
                        foreach (var dep in deps)
                        {
                            if (!IsDependencies(dep))
                            {
                                //TODO: add to dict count
                                extractedAssetPaths.Add(dep);
                            }
                        }
                    }
                }
            }
        }
    }
}