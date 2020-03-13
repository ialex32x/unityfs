using System;
using System.IO;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderWindow : BaseEditorWindow
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

        public static void CreateAssetListData()
        {
            var index = 0;
            do
            {
                var filePath = "Assets/unityfs_asset_list" + (index > 0 ? "_" + (index++) : "") + ".asset";
                if (!File.Exists(filePath))
                {
                    var list = ScriptableObject.CreateInstance<AssetListData>();
                    AssetDatabase.CreateAsset(list, filePath);
                    AssetDatabase.Refresh();
                    return;
                }
            } while (true);
        }

        protected override void OnEnable()
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

        protected override void OnGUIDraw()
        {
            var margin = 5f;
            Block("Settings",
                () =>
                {
                    EditorGUI.BeginChangeCheck();
                    data.encryptionKey = EditorGUILayout.TextField("Password", data.encryptionKey);
                    // 中间输出目录
                    data.assetBundlePath = EditorGUILayout.TextField("AssetBundle Path", data.assetBundlePath);
                    data.zipArchivePath = EditorGUILayout.TextField("ZipArchive Path", data.zipArchivePath);
                    // 最终包输出目录
                    data.packagePath = EditorGUILayout.TextField("Package Path", data.packagePath);
                    if (EditorGUI.EndChangeCheck())
                    {
                        data.MarkAsDirty();
                    }
                });
            var autoRect = EditorGUILayout.GetControlRect(GUILayout.Height(1f));
            var treeViewTop = autoRect.yMax;
            var bottomHeight = 21f;
            var treeViewRect = new Rect(5, treeViewTop + margin, position.width - 10,
                position.height - treeViewTop - bottomHeight - margin * 3f);
            var bottomRect = new Rect(5, treeViewRect.yMax + margin, treeViewRect.width, bottomHeight);
            _treeView.OnContextMenu(treeViewRect);
            _treeView.OnGUI(treeViewRect);
            GUILayout.BeginArea(bottomRect);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Bundle"))
                {
                    data.bundles.Add(new BundleBuilderData.BundleInfo()
                    {
                        id = ++data.id,
                        name = $"bundle_{data.id}{BundleBuilderData.FileExt}",
                    });
                    _treeView.Reload();
                }

                if (GUILayout.Button("Add Asset List"))
                {
                    CreateAssetListData();
                }

                GUI.color = Color.red;
                if (GUILayout.Button("Delete"))
                {
                    _treeView.DeleteSelectedItems();
                }

                GUI.color = _GUIColor;

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Collapse All"))
                {
                    _treeView.CollapseAll();
                }

                if (GUILayout.Button("Expand All"))
                {
                    _treeView._ExpandAll();
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
                        BundleBuilder.Build(data, "", EditorUserBuildSettings.activeBuildTarget);
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