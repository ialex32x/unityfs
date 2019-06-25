using System;
using System.IO;
using System.Collections.Generic;

namespace Examples
{
    using UnityEngine;

    public interface IFileSystem
    {
        bool Exists(string filePath);
        byte[] ReadAllBytes(string filePath);
    }

    // file system layer for script engine
    public class FakeFileSystem
    {
        public bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }

        public byte[] ReadAllBytes(string filePath)
        {
            using (var stream = UnityFS.ResourceManager.OpenFile(filePath))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }
    }
}
