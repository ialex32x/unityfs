using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class FailureFileSystem : IFileSystem
    {
        private string _name;

        public string name
        {
            get { return _name; }
        }

        public event Action<IFileSystem> completed
        {
            add
            {
                value(this);
            }

            remove
            {
            }
        }

        public FailureFileSystem(string name)
        {
            _name = name;
        }

        public bool Exists(string filename)
        {
            return false;
        }

        public byte[] ReadAllBytes(string filename)
        {
            return null;
        }

        public Stream OpenRead(string filename)
        {
            return null;
        }
    }
}
