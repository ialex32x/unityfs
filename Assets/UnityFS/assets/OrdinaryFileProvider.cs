using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class OrdinaryFileProvider : IFileProvider
    {
        public Stream OpenFile(string filename)
        {
            return File.OpenRead(filename);
        }
    }
}
