using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleAssetsWindow : EditorWindow
    {
        private IList<BundleBuilderData.BundleInfo> _bundles;

        void OnEnable()
        {
            titleContent = new GUIContent("Bundle Assets");
        }

        public void SetBundles(IList<BundleBuilderData.BundleInfo> bundles)
        {
            _bundles = bundles;
        }

        void OnGUI()
        {
            if (_bundles == null || _bundles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }
            foreach (var bundle in _bundles)
            {
                var bundleName = string.IsNullOrEmpty(bundle.name) ? "(null)" : bundle.name;
                EditorGUILayout.HelpBox($"{bundleName}, {bundle.assets.Count} assets", MessageType.Info);
                foreach (var asset in bundle.assets)
                {
                    EditorGUILayout.BeginHorizontal();
                    var assetPath = string.Empty;
                    if (asset.target != null)
                    {
                        assetPath = AssetDatabase.GetAssetPath(asset.target);
                    }
                    EditorGUILayout.TextField(assetPath);
                    EditorGUILayout.ObjectField(asset.target, typeof(Object), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
