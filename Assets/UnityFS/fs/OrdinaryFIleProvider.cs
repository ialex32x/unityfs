using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class OrdinaryFileProvider : IFileProvider
    {
        private string _pathRoot;

        public OrdinaryFileProvider(string pathRoot)
        {
            _pathRoot = pathRoot;
        }

        public bool Exists(string filename)
        {
            var fullPath = Path.Combine(_pathRoot, filename);
            return File.Exists(fullPath);
        }

        public byte[] ReadAllBytes(string filename)
        {
            var fullPath = Path.Combine(_pathRoot, filename);
            // var stream = new System.IO.FileStream(fullPath, System.IO.FileMode.Open);
            return File.ReadAllBytes(fullPath);
        }
    }
}
