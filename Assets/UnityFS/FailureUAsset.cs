using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 代表无法加载的资源
    public class FailureUAsset : UAsset
    {
        public FailureUAsset(string assetPath)
        : base(assetPath)
        {
            Complete();
        }
    }
}
