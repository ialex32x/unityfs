using System;
using System.IO;
using System.Collections.Generic;
using UnityFS;

namespace Examples
{
    using UnityEngine;

    // 只是演示
    public class CompositeFileSystem
    {
        private List<UnityFS.IFileSystem> _fileSystems = new List<IFileSystem>();

        public CompositeFileSystem AddFileSystems(params IFileSystem[] fileSystems)
        {
            for (int i = 0, count = fileSystems.Length; i < count; i++)
            {
                _fileSystems.Add(fileSystems[i]);
            }

            return this;
        }
        
        public CompositeFileSystem AddFileSystem(IFileSystem fileSystem)
        {
            _fileSystems.Add(fileSystem);
            return this;
        }
        
        public bool Exists(string filename)
        {
            for (int i = 0, count = _fileSystems.Count; i < count; i++)
            {
                var fileSystem = _fileSystems[i];
                if (fileSystem.Exists(filename))
                {
                    return true;
                }
            }

            return false;
        }

        public byte[] ReadAllBytes(string filename)
        {
            for (int i = 0, count = _fileSystems.Count; i < count; i++)
            {
                var fileSystem = _fileSystems[i];
                if (fileSystem.Exists(filename))
                {
                    return fileSystem.ReadAllBytes(filename);
                }
            }

            return null;
        }
    }
}