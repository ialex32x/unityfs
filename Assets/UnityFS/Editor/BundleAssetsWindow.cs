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
        private BundleBuilderData.BundleInfo _bundle;

        void OnEnable()
        {
            titleContent = new GUIContent("Bundle Assets");
        }

        public void SetBundle(BundleBuilderData.BundleInfo bundle)
        {
            _bundle = bundle;
        }

        void OnGUI()
        {
            if (_bundle == null)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }
            var bundleName = string.IsNullOrEmpty(_bundle.name) ? "(null)" : _bundle.name;
            EditorGUILayout.HelpBox($"{bundleName}, {_bundle.assets.Count} assets", MessageType.Info);
            foreach (var asset in _bundle.assets)
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
