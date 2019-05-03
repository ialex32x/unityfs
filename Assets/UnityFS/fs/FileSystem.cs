using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class FileSystem
    {
        private List<IFileProvider> _providers = new List<IFileProvider>();

        public void AddFileProvider(fp IFileProvider) 
        {
            _providers.Add(fp);
        }
    }
}