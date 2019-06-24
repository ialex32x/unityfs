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
            var treeViewRect = new Rect(5, 5, position.width - 10, position.height - 36);
            _treeView.OnGUI(treeViewRect);
            var bottomRect = new Rect(5, treeViewRect.yMax + 5, treeViewRect.width, 21);
            GUILayout.BeginArea(bottomRect);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
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

            // var leftPanelWidth = 360f;
            // if (GUILayout.Button("Add Bundle", GUILayout.Width(leftPanelWidth)))
            // {
            //     data.bundles.Add(new BundleBuilderData.BundleInfo());
            // }

            // for (var bundleIndex = 0; bundleIndex < data.bundles.Count; bundleIndex++)
            // {
            //     var bundle = data.bundles[bundleIndex];
            //     var name = string.IsNullOrEmpty(bundle.name) ? "(noname)" : bundle.name;

            //     if (bundle == selected)
            //     {
            //     }
            //     else
            //     {
            //     }
            // }

            // if (selected != null)
            // {
            //     EditorGUI.BeginChangeCheck();
            //     selected.name = EditorGUILayout.TextField("Name", selected.name);
            //     // selected.targets
            //     if (EditorGUI.EndChangeCheck())
            //     {
            //         dirty = true;
            //     }
            // }

            // if (GUILayout.Button("Build"))
            // {
            //     if (BundleBuilder.Scan(data))
            //     {
            //         dirty = true;
            //     }
            //     BundleBuilder.Build(data, "out/AssetBundles", EditorUserBuildSettings.activeBuildTarget);
            // }

            if (dirty)
            {
                EditorUtility.SetDirty(data);
                dirty = false;
            }
        }
    }
}
