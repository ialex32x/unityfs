using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class ResourceManager
    {
        private List<IAssetProvider> _assetProviders = new List<IAssetProvider>();
        
        private List<IFileProvider> _fileProviders = new List<IFileProvider>();

        public void AddFileProvider(IFileProvider fp) 
        {
            _fileProviders.Add(fp);
        }
    }
}