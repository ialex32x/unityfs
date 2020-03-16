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
        public List<string> extractedAssetPaths = new List<string>(); // 展开所有依赖
        private List<PackageBuildEntry> _extractedDependencies = new List<PackageBuildEntry>();

        public void AddDependency(PackageBuildEntry entry)
        {
            if (entry != this)
            {
                if (!_dependencies.Contains(entry))
                {
                    _dependencies.Add(entry);
                }
            }
        }

        private void _AddDependencies(List<PackageBuildEntry> dependencies)
        {
            for (int i = 0, size = dependencies.Count; i < size; i++)
            {
                var depBundle = dependencies[i];
                if (!_extractedDependencies.Contains(depBundle))
                {
                    _extractedDependencies.Add(depBundle);
                    _AddDependencies(depBundle._dependencies);
                }
            }
        }

        public void Extract()
        {
            _AddDependencies(_dependencies);
            //TODO: TBD...
        }
    }
}