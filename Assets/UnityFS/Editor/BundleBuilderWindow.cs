using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderWindow : EditorWindow
    {
        [SerializeField] MultiColumnHeaderState _headerState;
        [SerializeField] TreeViewState _treeViewState = new TreeViewState();
        BundleBuilderTreeView _treeView;

        private bool dirty;
        private BundleBuilderData.BundleInfo selected;
        private BundleBuilderData data;

        [MenuItem("UnityFS/Builder")]
        public static void OpenBuilderWindow()
        {
            GetWindow<BundleBuilderWindow>().Show();
        }

        void OnEnable()
        {
            data = BundleBuilder.GetData();
            titleContent = new GUIContent("Bundle Builder");
            bool firstInit = _headerState == null;
            var headerState = BundleBuilderTreeView.CreateDefaultMultiColumnHeaderState(this.position.width);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(_headerState, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(_headerState, headerState);
            var header = new BundleBuilderTreeViewHeader(headerState);
            _headerState = headerState;

            _treeView = new BundleBuilderTreeView(_treeViewState, header);
            _treeView.SetData(data);
        }

        void OnGUI()
        {
            var topRect = new Rect(5, 5, position.width - 10, 21);
            GUILayout.BeginArea(topRect);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Bundle"))
                {
                    //TODO: add bundle 
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndArea();
            var treeViewRect = new Rect(5, 28, position.width - 10, position.height - 56);
            _treeView.OnGUI(treeViewRect);
            var bottomRect = new Rect(5, treeViewRect.yMax + 5, treeViewRect.width, 21);
            GUILayout.BeginArea(bottomRect);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Delete"))
                {
                    var bundlePending = new List<BundleBuilderData.BundleInfo>();
                    var targetPending = new List<BundleBuilderData.BundleAssetTarget>();
                    foreach (var bundle in data.bundles)
                    {
                        if (_treeView.IsSelected(bundle.id))
                        {
                            bundlePending.Add(bundle);
                        }
                        else
                        {
                            foreach (var target in bundle.targets)
                            {
                                if (_treeView.IsSelected(target.id))
                                {
                                    targetPending.Add(target);
                                }
                            }
                        }
                    }
                    if (bundlePending.Count == 0 && targetPending.Count == 0)
                    {
                        if (EditorUtility.DisplayDialog("删除", "没有选中任何资源.", "确定"))
                        {
                        }
                    }
                    else
                    {
                        if (EditorUtility.DisplayDialog("删除", $"确定删除 {bundlePending.Count} 个整资源包以及 {targetPending.Count} 项资源?", "删除", "取消"))
                        {
                            foreach (var bundle in bundlePending)
                            {
                                data.bundles.Remove(bundle);
                            }
                            foreach (var bundle in data.bundles)
                            {
                                foreach (var target in targetPending)
                                {
                                    bundle.targets.Remove(target);
                                }
                            }
                            dirty = true;
                            _treeView.Reload();
                        }
                    }
                }
                GUILayout.FlexibleSpace();
                // if (GUILayout.Button("Expand All"))
                // {
                //     _treeView.ExpandAll();
                // }
                if (GUILayout.Button("Collapse All"))
                {
                    _treeView.CollapseAll();
                }
                GUILayout.Space(20f);
                if (GUILayout.Button("Refresh"))
                {
                    _treeView.Reload();
                }
                if (GUILayout.Button("Show Bundle Assets"))
                {
                    BundleBuilderData.BundleInfo selectedBundle = null;
                    foreach (var bundle in data.bundles)
                    {
                        if (_treeView.IsSelected(bundle.id))
                        {
                            selectedBundle = bundle;
                            break;
                        }
                        foreach (var asset in bundle.targets)
                        {
                            if (_treeView.IsSelected(asset.id))
                            {
                                selectedBundle = bundle;
                                break;
                            }
                        }
                        if (selectedBundle != null)
                        {
                            break;
                        }
                    }
                    if (selectedBundle != null)
                    {
                        if (BundleBuilder.Scan(data))
                        {
                            dirty = true;
                        }
                        var win = GetWindow<BundleAssetsWindow>();
                        win.SetBundle(selectedBundle);
                        win.Show();
                    }
                    else
                    {
                        Debug.LogWarning("no bundle selected");
                    }
                }
                if (GUILayout.Button("Build"))
                {
                    if (BundleBuilder.Scan(data))
                    {
                        dirty = true;
                    }
                    BundleBuilder.Build(data, "out/AssetBundles", EditorUserBuildSettings.activeBuildTarget);
                }
            }
            GUILayout.EndArea();

            if (dirty)
            {
                EditorUtility.SetDirty(data);
                dirty = false;
            }
        }
    }
}
