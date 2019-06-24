using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public abstract class BundleBuilderTreeViewItem : TreeViewItem
    {
        public abstract bool enabled
        {
            get;
            set;
        }

        public BundleBuilderTreeViewItem(int id, int depth, string displayName)
                : base(id, depth, displayName)
        {
        }
    }

    public class BundleBuilderTreeViewRoot : TreeViewItem
    {
        public BundleBuilderTreeViewRoot(int id, int depth, string displayName)
        : base(id, depth, displayName)
        {
        }
    }

    public class BundleBuilderTreeViewBundle : BundleBuilderTreeViewItem
    {
        public BundleBuilderData.BundleInfo bundleInfo;

        public override bool enabled
        {
            get { return bundleInfo.enabled; }
            set { bundleInfo.enabled = value; }
        }

        public BundleBuilderTreeViewBundle(int id, int depth, string displayName, BundleBuilderData.BundleInfo bundleInfo)
        : base(id, depth, displayName)
        {
            this.bundleInfo = bundleInfo;
        }
    }

    public class BundleBuilderTreeViewTarget : BundleBuilderTreeViewItem
    {
        public BundleBuilderData.BundleAssetTarget assetTarget;

        public override bool enabled
        {
            get { return assetTarget.enabled; }
            set { assetTarget.enabled = value; }
        }

        public BundleBuilderTreeViewTarget(int id, int depth, string displayName, BundleBuilderData.BundleAssetTarget assetTarget)
        : base(id, depth, displayName)
        {
            this.assetTarget = assetTarget;
        }
    }
}
