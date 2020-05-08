using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 代表无法加载的资源
    public class UFailureAsset : UAsset
    {
        public UFailureAsset(string assetPath, Type type)
        : base(assetPath, type)
        {
            Complete();
        }

        protected override bool IsValid()
        {
            return false;
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
