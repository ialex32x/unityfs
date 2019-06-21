using System;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS
{
    using UnityEngine;

    // 访问 zip 文件
    public class ZipFileSystem
    {
        private ZipFile _zipFile;

        private ZipFileSystem(ZipFile zipFile)
        {
            _zipFile = zipFile;
        }

        public static ZipFileSystem CreateFromBytes(byte[] fileBytes)
        {
            var zipFile = new ZipFile(new MemoryStream(fileBytes));
            zipFile.IsStreamOwner = true;
            var provider = new ZipFileSystem(zipFile);
            return provider;
        }

        // stream 生命周期将由 zipfile 接管
        public static ZipFileSystem CreateFromStream(Stream stream)
        {
            var zipFile = new ZipFile(stream);
            zipFile.IsStreamOwner = true;
            var provider = new ZipFileSystem(zipFile);
            return provider;
        }

        public bool Exists(string filename)
        {
            if (_zipFile != null)
            {
                var entry = _zipFile.FindEntry(filename, false);
                if (entry >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public byte[] ReadAllBytes(string filename)
        {
            if (_zipFile != null)
            {
                var entry = _zipFile.GetEntry(filename);
                if (entry != null)
                {
                    using (var stream = _zipFile.GetInputStream(entry))
                    {
                        var buffer = new byte[entry.Size];
                        stream.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }
                }
            }
            return null;
        }
    }
}
