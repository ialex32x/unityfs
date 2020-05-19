using System.Collections.Generic;
using System.IO;

namespace UnityFS.Editor
{
    public class SearchResult
    {
        public BundleBuilderData.BundleInfo bundleInfo;
        public BundleBuilderData.BundleSplit bundleSplit;
        public BundleBuilderData.BundleSlice bundleSlice;

        public string assetPath;
        public string assetGuid;
    }
}
