using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetsAnalyzer
    {
        void OnAssetOpen(string assetPath);
        void OnAssetAccess(string assetPath);
        void OnAssetClose(string assetPath);
    }


    public class EmptyAssetsAnalyzer : IAssetsAnalyzer
    {
        public void OnAssetOpen(string assetPath)
        {
        }

        public void OnAssetAccess(string assetPath)
        {
        }

        public void OnAssetClose(string assetPath)
        {
        }
    }
}
