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
                    data.bundles.Add(new BundleBuilderData.BundleInfo()
                    {
                        id = ++data.id,
                        name = $"bundle_{data.id}{BundleBuilderData.Ext}",
                    });
                    _treeView.Reload();
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndArea();
            var treeViewRect = new Rect(5, 28, position.width - 10, position.height - 56);
            _treeView.OnContextMenu(treeViewRect);
            _treeView.OnGUI(treeViewRect);
            var bottomRect = new Rect(5, treeViewRect.yMax + 5, treeViewRect.width, 21);
            GUILayout.BeginArea(bottomRect);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Delete"))
                {
                    _treeView.DeleteSelectedItems();
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
                    _treeView.ShowBundleReport();
                }
                if (GUILayout.Button("Build"))
                {
                    EditorApplication.delayCall += () =>
                    {
                        BundleBuilder.Build(data, "out/AssetBundles", EditorUserBuildSettings.activeBuildTarget);
                    };
                }
            }
            GUILayout.EndArea();

            if (dirty)
            {
                data.MarkAsDirty();
                dirty = false;
            }
        }
    }
}
