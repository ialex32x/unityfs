using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // read from Resources (无法验证版本)
    public class BuiltinAssetProvider : IAssetProvider
    {
        public UAsset GetAsset(string assetPath)
        {
            throw new NotImplementedException();
        }
    }
}
