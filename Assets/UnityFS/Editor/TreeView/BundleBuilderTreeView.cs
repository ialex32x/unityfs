using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderTreeView : TreeView
    {
        private const float kRowHeights = 20f;
        private const float kToggleWidth = 18f;
        static Texture2D kIconDelete1 = EditorGUIUtility.FindTexture("d_AS Badge Delete");
        static GUIContent kIconPrefab = EditorGUIUtility.IconContent("Prefab Icon");
        static Texture2D kIconFolder = EditorGUIUtility.FindTexture("Folder Icon");
        static Texture2D kIconFavorite = EditorGUIUtility.FindTexture("Favorite Icon");
        static Texture2D kIconSceneAsset = EditorGUIUtility.FindTexture("BuildSettings.Editor");
        static Texture2D kIconZipArchive = EditorGUIUtility.FindTexture("MetaFile Icon");
        static GUIContent kIconTextAsset = EditorGUIUtility.IconContent("TextAsset Icon");
        static GUIContent kIconTexture = EditorGUIUtility.IconContent("Texture Icon");
        static GUIContent kIconMaterial = EditorGUIUtility.IconContent("Material Icon");
        static GUIContent kIconDelete2 = EditorGUIUtility.IconContent("d_P4_DeletedLocal");

        private BundleBuilderData _data;

        public BundleBuilderTreeView(TreeViewState state) : base(state)
        {
        }

        public BundleBuilderTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 2;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f;
            extraSpaceBeforeIconAndLabel = kToggleWidth;
            multiColumnHeader.sortingChanged += OnSortingChanged;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByLabel"), "-"),
                    contextMenuText = "Asset",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 30,
                    minWidth = 30,
                    maxWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType"), "-"),
                    contextMenuText = "Type",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 30,
                    minWidth = 30,
                    maxWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Assets"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 150,
                    minWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("StreamingAssets"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 110,
                    minWidth = 60,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Load/Filter"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 95,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Priority"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 70,
                    minWidth = 60,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Split"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 70,
                    minWidth = 60,
                    autoResize = true
                }
            };

            // Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");
            var state = new MultiColumnHeaderState(columns);
            return state;
        }

        public void SetData(BundleBuilderData data)
        {
            _data = data;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            return new BundleBuilderTreeViewRoot(0, -1, "root");
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = new List<TreeViewItem>();
            if (_data != null)
            {
                foreach (var bundle in _data.bundles)
                {
                    var bundleName = string.IsNullOrEmpty(bundle.name) ? "(noname)" : bundle.name;
                    var bundleTV = new BundleBuilderTreeViewBundle(bundle.id, 0, bundleName, bundle);
                    rows.Add(bundleTV);
                    if (IsExpanded(bundleTV.id))
                    {
                        AddChildrenRecursive(rows, bundle, bundleTV);
                    }
                    else
                    {
                        bundleTV.children = CreateChildListForCollapsedParent();
                    }
                }
                SetupParentsAndChildrenFromDepths(root, rows);
            }
            return rows;
        }

        protected void AddChildrenRecursive(List<TreeViewItem> rows, BundleBuilderData.BundleInfo bundleInfo, BundleBuilderTreeViewBundle node)
        {
            foreach (var target in bundleInfo.targets)
            {
                string name = "(null)";
                if (target.target != null)
                {
                    name = target.target.name;
                }
                var tv = new BundleBuilderTreeViewTarget(target.id, 1, name, target);
                rows.Add(tv);
                if (IsExpanded(tv.id))
                {
                }
                else
                {
                    // tv.children = CreateChildListForCollapsedParent();
                }
            }
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (BundleBuilderTreeViewItem)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        public void OnContextMenu(Rect treeViewRect)
        {
            var evt = Event.current;
            if (evt.type == EventType.ContextClick)
            {
                var mousePos = evt.mousePosition;
                treeViewRect.yMin += multiColumnHeader.height;
                if (treeViewRect.Contains(mousePos))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("查看资源报告"), false, OnContextMenuInspect);
                    // menu.AddItem(new GUIContent("Not Implemented 2"), false, OnContextMenuTest);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("删除"), false, OnContextMenuDelete);
                    menu.ShowAsContext();
                    evt.Use();
                }
            }
        }

        private void OnContextMenuInspect()
        {
            ShowBundleReport();
        }

        private void OnContextMenuDelete()
        {
            DeleteSelectedItems();
        }

        public void ShowBundleReport()
        {
            var selectedBundles = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in _data.bundles)
            {
                if (this.IsSelected(bundle.id))
                {
                    selectedBundles.Add(bundle);
                    continue;
                }
                foreach (var asset in bundle.targets)
                {
                    if (this.IsSelected(asset.id))
                    {
                        selectedBundles.Add(bundle);
                        break;
                    }
                }
            }
            if (selectedBundles.Count != 0)
            {
                var win = EditorWindow.GetWindow<BundleAssetsWindow>();
                win.SetBundles(_data, selectedBundles);
                win.Show();
            }
            else
            {
                Debug.LogWarning("no bundle selected");
            }
        }

        public void DeleteSelectedItems()
        {
            var bundlePending = new List<BundleBuilderData.BundleInfo>();
            var targetPending = new List<BundleBuilderData.BundleAssetTarget>();
            foreach (var bundle in _data.bundles)
            {
                if (this.IsSelected(bundle.id))
                {
                    bundlePending.Add(bundle);
                }
                else
                {
                    foreach (var target in bundle.targets)
                    {
                        if (this.IsSelected(target.id))
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
                if (EditorUtility.DisplayDialog("删除", $"确定删除选中的 {bundlePending.Count} 个整资源包以及 {targetPending.Count} 项资源?", "删除", "取消"))
                {
                    foreach (var bundle in bundlePending)
                    {
                        _data.bundles.Remove(bundle);
                    }
                    foreach (var bundle in _data.bundles)
                    {
                        foreach (var target in targetPending)
                        {
                            bundle.targets.Remove(target);
                        }
                    }
                    _data.MarkAsDirty();
                    this.Reload();
                }
            }
        }

        private void CellGUI(Rect cellRect, BundleBuilderTreeViewItem item, int column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        if (bundleInfo.type == Manifest.BundleType.AssetBundle)
                        {
                            GUI.DrawTexture(cellRect, kIconFavorite, ScaleMode.ScaleToFit);
                        }
                        // else if (bundleInfo.type == BundleType.SceneBundle)
                        // {
                        //     GUI.DrawTexture(cellRect, kIconSceneAsset, ScaleMode.ScaleToFit);
                        // }
                        else
                        {
                            GUI.DrawTexture(cellRect, kIconZipArchive, ScaleMode.ScaleToFit);
                        }
                    }
                    break;
                case 1:
                    if (item.depth == 1)
                    {
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.target != null)
                        {
                            if (target.target is GameObject)
                            {
                                GUI.DrawTexture(cellRect, kIconPrefab.image, ScaleMode.ScaleToFit);
                            }
                            else if (target.target is TextAsset)
                            {
                                GUI.DrawTexture(cellRect, kIconTextAsset.image, ScaleMode.ScaleToFit);
                            }
                            else if (target.target is Texture)
                            {
                                GUI.DrawTexture(cellRect, kIconTexture.image, ScaleMode.ScaleToFit);
                            }
                            else if (target.target is Material)
                            {
                                GUI.DrawTexture(cellRect, kIconMaterial.image, ScaleMode.ScaleToFit);
                            }
                            else if (target.target is SceneAsset)
                            {
                                GUI.DrawTexture(cellRect, kIconSceneAsset, ScaleMode.ScaleToFit);
                            }
                            else
                            {
                                var assetPath = AssetDatabase.GetAssetPath(target.target);
                                if (Directory.Exists(assetPath))
                                {
                                    GUI.DrawTexture(cellRect, kIconFolder, ScaleMode.ScaleToFit);
                                }
                            }
                        }
                    }
                    break;
                case 2:
                    // Do toggle
                    var toggleRect = cellRect;
                    var indent = GetContentIndent(item);
                    toggleRect.x += indent;
                    toggleRect.width = kToggleWidth;
                    if (toggleRect.xMax < cellRect.xMax)
                    {
                        item.enabled = EditorGUI.Toggle(toggleRect, item.enabled); // hide when outside cell rect
                    }
                    // Default icon and label
                    args.rowRect = cellRect;
                    cellRect.xMin += indent + kToggleWidth + 2f;
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        if (string.IsNullOrEmpty(bundleInfo.note))
                        {
                            EditorGUI.LabelField(cellRect, bundleInfo.name);
                        }
                        else
                        {
                            EditorGUI.LabelField(cellRect, bundleInfo.note);
                        }
                    }
                    else if (item.depth == 1)
                    {
                        var assetTarget = (item as BundleBuilderTreeViewTarget).assetTarget;
                        var target = EditorGUI.ObjectField(cellRect, GUIContent.none, assetTarget.target, typeof(Object), false);
                        if (target != assetTarget.target)
                        {
                            assetTarget.target = target;
                            _data.MarkAsDirty();
                        }
                    }
                    else
                    {
                        base.RowGUI(args);
                    }
                    break;
                case 3:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        if (bundleInfo != null)
                        {
                            var streamingAssets = EditorGUI.Toggle(cellRect, bundleInfo.streamingAssets);
                            if (streamingAssets != bundleInfo.streamingAssets)
                            {
                                bundleInfo.streamingAssets = streamingAssets;
                                _data.MarkAsDirty();
                            }
                            
                            // var platforms = (BundleAssetPlatforms)EditorGUI.EnumFlagsField(cellRect, bundleInfo.platforms);
                            // if (platforms != bundleInfo.platforms)
                            // {
                            //     bundleInfo.platforms = platforms;
                            //     _data.MarkAsDirty();
                            // }
                        }
                    }
                    else if (item.depth == 1)
                    {
                    }
                    break;
                case 4:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        cellRect.width *= 0.5f;
                        var load = (BundleLoad)EditorGUI.EnumPopup(cellRect, bundleInfo.load);
                        if (load != bundleInfo.load)
                        {
                            bundleInfo.load = load;
                            _data.MarkAsDirty();
                        }
                        cellRect.x += cellRect.width;
                        var type = (Manifest.BundleType)EditorGUI.EnumPopup(cellRect, bundleInfo.type);
                        if (type != bundleInfo.type)
                        {
                            bundleInfo.type = type;
                            _data.MarkAsDirty();
                            Reload();
                        }
                    }
                    else if (item.depth == 1)
                    {
                        // var bundleInfo = (item.parent as BundleBuilderTreeViewBundle).bundleInfo;
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.target != null)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(target.target);
                            if (Directory.Exists(assetPath))
                            {
                                var types = (BundleAssetTypes)EditorGUI.EnumFlagsField(cellRect, target.types);
                                if (types != target.types)
                                {
                                    target.types = types;
                                    _data.MarkAsDirty();
                                }
                            }
                        }
                    }
                    break;
                case 5:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var priority = EditorGUI.IntSlider(cellRect, bundleInfo.priority, 0, 10000);
                        if (priority != bundleInfo.priority)
                        {
                            bundleInfo.priority = priority;
                            _data.MarkAsDirty();
                        }
                    }
                    else if (item.depth == 1)
                    {
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.target != null)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(target.target);
                            if (Directory.Exists(assetPath))
                            {
                                var extensions = EditorGUI.TextField(cellRect, target.extensions);
                                if (extensions != target.extensions)
                                {
                                    target.extensions = extensions;
                                    _data.MarkAsDirty();
                                }
                            }
                        }
                    }
                    break;
                case 6:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        if (bundleInfo.type == Manifest.BundleType.AssetBundle)
                        {
                            var splitObjects = EditorGUI.IntSlider(cellRect, bundleInfo.splitObjects, 0, 100);
                            if (splitObjects != bundleInfo.splitObjects)
                            {
                                bundleInfo.splitObjects = splitObjects;
                                _data.MarkAsDirty();
                            }
                        }
                        // else if (bundleInfo.type == BundleType.SceneBundle)
                        // {
                        // }
                    }
                    break;
            }
        }

        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            // SortIfNeeded(rootItem, GetRows());
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (args.dragAndDropPosition != DragAndDropPosition.UponItem)
            {
                return DragAndDropVisualMode.None;
            }
            if (args.performDrop)
            {
                var draggedObjects = DragAndDrop.objectReferences;
                var tv = args.parentItem;
                if (tv.depth == 0)
                {
                    BundleBuilder.Add(_data, (tv as BundleBuilderTreeViewBundle).bundleInfo, draggedObjects);
                    Reload();
                }
            }
            return DragAndDropVisualMode.Move;
        }
    }
}
