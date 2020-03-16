using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 打包过程数据
    public partial class PackageBuildInfo
    {
        private Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public class Entry
        {
            public Manifest.BundleInfo _bundle;
            public HashSet<string> _assets = new HashSet<string>();
            public HashSet<string> _results = new HashSet<string>();

            public Entry(Manifest.BundleInfo bundle)
            {
                _bundle = bundle;
            }

            private bool IsValid(string assetPath)
            {
                return !assetPath.EndsWith(".cs") && !assetPath.EndsWith(".dll");
            }

            public void Add(string assetPath)
            {
                if (IsValid(assetPath) && _assets.Add(assetPath))
                {
                    var dependencies = AssetDatabase.GetDependencies(assetPath);
                    foreach (var dep in dependencies)
                    {
                        if (IsValid(dep) && _assets.Add(dep))
                        {
                        }
                    }
                }
            }
        }

        public Entry CreateEntry(Manifest.BundleInfo bundle)
        {
            Entry entry = new Entry(bundle);
            foreach (var assetPath in bundle.assets)
            {
                entry.Add(assetPath);
            }
            _entries.Add(bundle.name, entry);
            return entry;
        }

        private Entry GetEntry(string name)
        {
            return _entries.TryGetValue(name, out var entry) ? entry : null;
        }

        private void _AddDependencies(string[] depRefs, HashSet<Entry> deps)
        {
            for (int i = 0, size = depRefs.Length; i < size; i++)
            {
                var dep = GetEntry(depRefs[i]);
                if (deps.Add(dep))
                {
                    _AddDependencies(dep._bundle.dependencies, deps);
                }
            }
        }

        private List<KeyValuePair<Entry, string>> GetOverlaps(Entry self)
        {
            var overlaps = new List<KeyValuePair<Entry, string>>();
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                if (entry != self)
                {
                    foreach (var entryAsset in entry._assets)
                    {
                        if (self._assets.Contains(entryAsset))
                        {
                            var pair = new KeyValuePair<Entry, string>(entry, entryAsset);
                            overlaps.Add(pair);
                        }
                    }
                }
            }

            return overlaps;
        }

        public string Join(string[] values)
        {
            var s = "";
            if (values.Length > 0)
            {
                s += "[";
                for (var i = 0; i < values.Length; i++)
                {
                    s += values[i];
                    if (i != values.Length - 1)
                    {
                        s += ", ";
                    }
                }
                s += "]";
            }

            return s;
        }

        public void DoAnalyze()
        {
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                var deps = new HashSet<Entry>();
                var depAssets = new HashSet<string>();
                _AddDependencies(entry._bundle.dependencies, deps);
                foreach (var dep in deps)
                {
                    foreach (var depAsset in dep._assets)
                    {
                        depAssets.Add(depAsset);
                    }
                }

                entry._assets.ExceptWith(depAssets);
                // Debug.LogFormat("report {0} {1}:", kv.Key, Join(entry._bundle.dependencies));
                // foreach (var asset in entry._assets)
                // {
                //     Debug.LogFormat("        {0}", asset);
                // }
            }

            var sb = new StringBuilder();
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                var overlaps = GetOverlaps(entry);
                if (overlaps.Count > 0)
                {
                    sb.AppendFormat("可能存在资源冗余 Bundle: {0} {1} ...\n", kv.Key, entry._bundle.comment);
                    foreach (var pair in overlaps)
                    {
                        sb.AppendFormat("    in Bundle: {0} {1} => {2}\n", pair.Key._bundle.name, pair.Key._bundle.comment, pair.Value);
                    }

                    Debug.LogWarningFormat(sb.ToString());
                    sb.Clear();
                }
            }
        }
    }
}