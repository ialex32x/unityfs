using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IAssetProviderListener
    {
        void OnSetManifest();
        void OnStartupTask(Manifest.BundleInfo[] bundles);
        void OnTaskStart(ITask task);
        void OnTaskComplete(ITask task);
    }

    public class EmptyAssetProviderListener : IAssetProviderListener
    {
        public void OnSetManifest()
        {
        }

        public void OnStartupTask(Manifest.BundleInfo[] bundles)
        {
        }

        public void OnTaskComplete(ITask task)
        {
        }

        public void OnTaskStart(ITask task)
        {
        }
    }
}
