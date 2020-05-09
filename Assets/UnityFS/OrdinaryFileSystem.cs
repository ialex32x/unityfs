using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class OrdinaryFileSystem : AbstractFileSystem
    {
        private string _rootPath;

        public OrdinaryFileSystem() : this(null)
        {
        }

        public OrdinaryFileSystem(string rootPath)
        {
            _rootPath = rootPath;
            _loaded = true;
        }

        public override bool Exists(string filename)
        {
            if (string.IsNullOrEmpty(_rootPath))
            {
                return File.Exists(filename);
            }
            return File.Exists(Path.Combine(_rootPath, filename));
        }

        public override Stream OpenRead(string filename)
        {
            if (string.IsNullOrEmpty(_rootPath))
            {
                return File.OpenRead(filename);
            }
            return File.OpenRead(Path.Combine(_rootPath, filename));
        }

        public override byte[] ReadAllBytes(string filename)
        {
            if (string.IsNullOrEmpty(_rootPath))
            {
                return File.ReadAllBytes(filename);
            }
            return File.ReadAllBytes(Path.Combine(_rootPath, filename));
        }
    }
}
