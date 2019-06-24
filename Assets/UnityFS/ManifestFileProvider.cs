using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class ManifestFileProvider : IFileProvider
    {
        private string _pathRoot;

        private Manifest _manifest;

        public ManifestFileProvider(Manifest manifest, string pathRoot)
        {
            _pathRoot = pathRoot;
            _manifest = manifest;
        }

        public Stream OpenFile(string filename)
        {
            Manifest.BundleInfo bundleInfo;
            if (_manifest.bundles.TryGetValue(filename, out bundleInfo))
            {
                var fullPath = Path.Combine(_pathRoot, filename);
                var metaPath = fullPath + ".meta";
                if (File.Exists(fullPath) && File.Exists(metaPath))
                {
                    var json = File.ReadAllText(metaPath);
                    var metadata = JsonUtility.FromJson<Metadata>(json);
                    // quick but unsafe
                    if (metadata.checksum == bundleInfo.checksum && metadata.size == bundleInfo.size)
                    {
                        var stream = System.IO.File.OpenRead(fullPath);
                        return stream;
                    }
                }
            }
            return null;
        }
    }
}
