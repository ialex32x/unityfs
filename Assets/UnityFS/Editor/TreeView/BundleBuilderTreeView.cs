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
        static GUIContent kIconPrefab = EditorGUIUtility.IconContent("Prefab Icon");
        static Texture2D kIconFolder = EditorGUIUtility.FindTexture("Folder Icon");
        static Texture2D kIconFavorite = EditorGUIUtility.FindTexture("Favorite Icon");
        static Texture2D kIconSceneAsset = EditorGUIUtility.FindTexture("BuildSettings.Editor");
        static Texture2D kIconZipArchive = EditorGUIUtility.FindTexture("MetaFile Icon");
        static GUIContent kIconTextAsset = EditorGUIUtility.IconContent("TextAsset Icon");
        static GUIContent kIconTexture = EditorGUIUtility.IconContent("Texture Icon");
        static GUIContent kIconMaterial = EditorGUIUtility.IconContent("Material Icon");

        private BundleBuilderData _data;

        public BundleBuilderTreeView(TreeViewState state) : base(state)
        {
        }

        public BundleBuilderTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 1;
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
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByLabel"), "AssetPath"),
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
                    headerContent = new GUIContent("Assets"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 250,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType"), "-"),
                    contextMenuText = "Type",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 300,
                    minWidth = 30,
                    // maxWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("StreamingAssets?/Load/Filter"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 600,
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
                    width = 270,
                    minWidth = 60,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Build Order"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Left,
                    width = 70,
                    minWidth = 60,
                    autoResize = true
                },
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

        public void _ExpandAll()
        {
            foreach (var child in rootItem.children)
            {
                SetExpanded(child.id, true);
            }
        }

        private List<BundleBuilderData.BundleInfo> GetSortedBundleInfos(List<BundleBuilderData.BundleInfo> bundleInfos)
        {
            var list = new List<BundleBuilderData.BundleInfo>(bundleInfos);
            list.Sort((a, b) => a.buildOrder - b.buildOrder);
            return list;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = new List<TreeViewItem>();
            if (_data != null)
            {
                var sorted = GetSortedBundleInfos(_data.bundles);
                for (var i = 0; i < sorted.Count; i++)
                {
                    var bundle = sorted[i];
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
                var targetPath = target.targetPath ?? "";
                var name = "(null)";
                if (targetPath.StartsWith("Assets/"))
                {
                    var assetObject = AssetDatabase.LoadMainAssetAtPath(targetPath);
                    if (assetObject != null)
                    {
                        name = assetObject.name;
                    }
                }
                else
                {
                    name = targetPath;
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
                    menu.AddItem(new GUIContent("添加资源"), false, OnContextMenuAddAsset);
                    menu.AddItem(new GUIContent("删除"), false, OnContextMenuDelete);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("查看详细信息"), false, OnContextMenuInspect);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("全部展开"), false, OnContextMenuExpand);
                    menu.AddItem(new GUIContent("全部折叠"), false, OnContextMenuCollapse);
                    menu.ShowAsContext();
                    evt.Use();
                }
            }
        }

        private void OnContextMenuExpand()
        {
            _ExpandAll();
        }

        private void OnContextMenuCollapse()
        {
            CollapseAll();
        }

        private void OnContextMenuInspect()
        {
            ShowBundleReport();
        }

        private void OnContextMenuAddAsset()
        {
            var selectedBundles = new List<BundleBuilderData.BundleInfo>();
            foreach (var bundle in _data.bundles)
            {
                if (this.IsSelected(bundle.id))
                {
                    selectedBundles.Add(bundle);
                }
            }

            if (selectedBundles.Count != 0)
            {
                foreach (var bundle in selectedBundles)
                {
                    bundle.targets.Add(new BundleBuilderData.BundleAssetTarget()
                    {
                        enabled = true,
                        targetPath = "",
                    });
                }

                _data.MarkAsDirty();
                this.Reload();
            }
            else
            {
                Debug.LogWarning("no bundle selected");
            }
        }

        private void OnContextMenuDelete()
        {
            DeleteSelectedItems();
        }

        public void ShowBundleReport()
        {
            // BundleBuilder.Scan(_data);
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
                BundleAssetsWindow.Inspect(_data, selectedBundles);
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
                        else
                        {
                            GUI.DrawTexture(cellRect, kIconZipArchive, ScaleMode.ScaleToFit);
                        }
                    }
                    break;
                case 1:
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
                            EditorGUI.LabelField(cellRect, string.Format("{0} ({1})", bundleInfo.note, bundleInfo.name));
                        }
                    }
                    else if (item.depth == 1)
                    {
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.targetPath.StartsWith("Assets/"))
                        {
                            var assetObject = AssetDatabase.LoadMainAssetAtPath(target.targetPath);
                            var newAssetObject = EditorGUI.ObjectField(cellRect, GUIContent.none, assetObject, typeof(Object), false);
                            if (newAssetObject != assetObject)
                            {
                                target.targetPath = newAssetObject != null ? AssetDatabase.GetAssetPath(newAssetObject) : string.Empty;
                                _data.MarkAsDirty();
                            }
                        }
                        else
                        {
                            var oldColor = GUI.color;
                            var bundleInfo = (item.parent as BundleBuilderTreeViewBundle).bundleInfo;
                            if (bundleInfo.type == Manifest.BundleType.ZipArchive)
                            {
                                if (Directory.Exists(target.targetPath) || File.Exists(target.targetPath))
                                {
                                    GUI.color = Color.yellow;
                                    GUI.Label(cellRect, "<External>");
                                }
                                else
                                {
                                    GUI.color = Color.red;
                                    GUI.Label(cellRect, "<Invalid>");
                                }
                            }
                            else
                            {
                                GUI.color = Color.red;
                                GUI.Label(cellRect, "<Not Supported>");
                            }
                            GUI.color = oldColor;
                        }
                    }
                    else
                    {
                        base.RowGUI(args);
                    }
                    break;
                case 2:
                    if (item.depth == 1)
                    {
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        var newTargetPath = GUI.TextField(cellRect, target.targetPath);

                        if (newTargetPath != target.targetPath)
                        {
                            target.targetPath = newTargetPath;
                            _data.MarkAsDirty();
                        }
                    }
                    break;
                case 3:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var popupWidth = (cellRect.width - 20f) * 0.5f;
                        cellRect.width = 20f;
                        var streamingAssets = EditorGUI.Toggle(cellRect, bundleInfo.streamingAssets);
                        if (streamingAssets != bundleInfo.streamingAssets)
                        {
                            bundleInfo.streamingAssets = streamingAssets;
                            _data.MarkAsDirty();
                        }
                        cellRect.x += cellRect.width;
                        cellRect.width = popupWidth;
                        var load = (Manifest.BundleLoad)EditorGUI.EnumPopup(cellRect, bundleInfo.load);
                        if (load != bundleInfo.load)
                        {
                            bundleInfo.load = load;
                            _data.MarkAsDirty();
                        }
                        cellRect.x += cellRect.width;
                        cellRect.width = popupWidth;
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
                    }
                    break;
                case 4:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var priority = EditorGUI.IntSlider(cellRect, bundleInfo.priority, 0, _data.priorityMax);
                        if (priority != bundleInfo.priority)
                        {
                            bundleInfo.priority = priority;
                            _data.MarkAsDirty();
                        }
                    }
                    else if (item.depth == 1)
                    {
                    }
                    break;
                case 5:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var buildOrder = EditorGUI.IntField(cellRect, bundleInfo.buildOrder);
                        if (buildOrder != bundleInfo.buildOrder)
                        {
                            bundleInfo.buildOrder = buildOrder;
                            _data.MarkAsDirty();
                        }
                    }
                    break;
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            // ShowBundleReport();
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
