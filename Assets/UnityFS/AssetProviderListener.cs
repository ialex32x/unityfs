using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProviderListener
    {
        void OnProgress(int taskIndex, int taskCount, ITask task, Manifest.BundleInfo[] bundles);
        void OnComplete();
        void OnTaskProgress(ITask task);
        void OnTaskComplete(ITask task);
    }
}
