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
        private BundleBuilderData _data;
        private IList<BundleBuilderData.BundleInfo> _bundles;
        private Vector2 _sv;

        private BuildTarget _targetPlatform;

        void OnEnable()
        {
            titleContent = new GUIContent("Bundle Report");
        }

        public void SetBundles(BundleBuilderData data, IList<BundleBuilderData.BundleInfo> bundles)
        {
            _data = data;
            _bundles = bundles;
            _targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            BundleBuilder.Scan(_data, _targetPlatform);
        }

        void OnGUI()
        {
            if (_bundles == null || _bundles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }
            _sv = GUILayout.BeginScrollView(_sv);
            var rescan = false;
            GUILayout.BeginHorizontal();
            var targetPlatform = (BuildTarget)EditorGUILayout.EnumPopup("Preview Platform", _targetPlatform);
            if (GUILayout.Button("Reset", GUILayout.Width(120f)))
            {
                _targetPlatform = targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
            {
                rescan = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(20f);
            if (_targetPlatform != targetPlatform)
            {
                _targetPlatform = targetPlatform;
                rescan = true;
            }
            if (rescan)
            {
                BundleBuilder.Scan(_data, _targetPlatform);
            }
            foreach (var bundle in _bundles)
            {
                var bundleName = string.IsNullOrEmpty(bundle.name) ? "(null)" : bundle.name;
                EditorGUILayout.HelpBox($"{bundleName}", MessageType.Info);
                var note = EditorGUILayout.TextField(bundle.note);
                if (note != bundle.note)
                {
                    bundle.note = note;
                    _data.MarkAsDirty();
                }
                GUILayout.Space(20f);
                for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
                {
                    var split = bundle.splits[splitIndex];
                    for (var assetIndex = 0; assetIndex < split.assets.Count; assetIndex++)
                    {
                        var asset = split.assets[assetIndex];
                        var assetPath = string.Empty;
                        if (asset != null)
                        {
                            assetPath = AssetDatabase.GetAssetPath(asset);
                        }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.IntField(splitIndex, GUILayout.Width(30f));
                        EditorGUILayout.TextField(assetPath);
                        EditorGUILayout.ObjectField(asset, typeof(Object), false);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                GUILayout.Space(50f);
            }
            GUILayout.EndScrollView();
        }
    }
}
