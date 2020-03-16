using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    // 打包过程数据
    public partial class PackageBuildInfo
    {
        public static HashSet<string> IgnoredAssetPaths = new HashSet<string>();
        
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
                return !IgnoredAssetPaths.Contains(assetPath) && !assetPath.EndsWith(".cs") && !assetPath.EndsWith(".dll");
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
            var overlapedAssets = new Dictionary<string, HashSet<Entry>>();
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                var overlaps = GetOverlaps(entry);
                if (overlaps.Count > 0)
                {
                    sb.AppendFormat("可能存在资源冗余 Bundle: {0} {1} ...\n", kv.Key, entry._bundle.comment);
                    foreach (var pair in overlaps)
                    {
                        HashSet<Entry> set;
                        if (!overlapedAssets.TryGetValue(pair.Value, out set))
                        {
                            overlapedAssets[pair.Value] = set = new HashSet<Entry>();
                        }

                        set.Add(pair.Key);
                        set.Add(entry);
                        sb.AppendFormat("    in Bundle: {0} {1} => {2}\n", pair.Key._bundle.name, pair.Key._bundle.comment, pair.Value);
                    }

                    Debug.LogWarningFormat(sb.ToString());
                    sb.Clear();
                }
            }

            sb.AppendFormat("<html><body>\n");
            sb.AppendFormat("<table border='1'>\n");
            foreach (var overlapedAsset in overlapedAssets)
            {
                var list = overlapedAsset.Value.ToArray();
                sb.AppendFormat("<tr>\n");
                sb.AppendFormat("<th rowspan='{0}'>{1}</th>\n", list.Length, overlapedAsset.Key);
                sb.AppendFormat("<td>{0}</td> <td>{1}</td>\n", list[0]._bundle.name, list[0]._bundle.comment);
                sb.AppendFormat("</tr>\n");
                for (var i = 1; i < list.Length; i++)
                {
                    sb.AppendFormat("<tr>\n");
                    sb.AppendFormat("<td>{0}</td> <td>{1}</td>\n", list[i]._bundle.name, list[i]._bundle.comment);
                    sb.AppendFormat("</tr>\n");
                }
            }
            sb.AppendFormat("</table>\n");
            sb.AppendFormat("</body></html>\n");
            File.WriteAllText(Path.Combine(_packagePath, "report.html"), sb.ToString());
            sb.Clear();
        }
    }
}