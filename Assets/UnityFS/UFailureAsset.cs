using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 代表无法加载的资源
    public class UFailureAsset : UAsset
    {
        public UFailureAsset(string assetPath)
        : base(assetPath)
        {
            Complete();
        }

        public override byte[] ReadAllBytes()
        {
            return null;
        }

        protected override void Dispose(bool bManaged)
        {
        }
    }
}
