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
            for (int i = 0, size = _manifest.bundles.Count; i < size; i++)
            {
                var bundleInfo = _manifest.bundles[i];
                if (bundleInfo.name == filename)
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
            }
            return null;
        }
    }
}
