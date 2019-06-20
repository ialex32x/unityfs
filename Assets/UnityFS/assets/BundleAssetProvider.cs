using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    // 资源包资源管理
    public class BundleAssetProvider : IAssetProvider
    {
        protected class UBundle
        {
            private AssetBundle _assetBundle;
        }

        protected class BundleUAsset : UAsset
        {
            private UBundle _bundle;

            public BundleUAsset(UBundle bundle, string assetPath)
            : base(assetPath)
            {
                _bundle = bundle;
            }

            public override bool Load()
            {
                throw new NotImplementedException();
            }

            public override Object LoadSync()
            {
                throw new NotImplementedException();
            }
        }

        // 资源路径 => 资源包 的快速映射
        private Dictionary<string, string> _assetPath2Bundle = new Dictionary<string, string>();

        public UAsset GetAsset(string assetPath)
        {
            throw new NotImplementedException();
        }
    }
}
