using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProviderListener
    {
        void OnStartupTask(Manifest.BundleInfo[] bundles);
        void OnComplete();
        void OnTaskStart(ITask task);
        void OnTaskComplete(ITask task);
    }
}
