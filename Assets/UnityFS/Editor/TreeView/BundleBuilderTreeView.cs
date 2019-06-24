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
        static Texture2D kIconDelete = EditorGUIUtility.FindTexture("d_AS Badge Delete");
        static Texture2D[] s_TestIcons =
        {
            EditorGUIUtility.FindTexture("Favorite Icon"),
            EditorGUIUtility.FindTexture("Folder Icon"),
            EditorGUIUtility.FindTexture("GameObject Icon"),
        };

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
                    headerContent = new GUIContent("Type"),
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

        private void CellGUI(Rect cellRect, BundleBuilderTreeViewItem item, int column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0:
                    if (item.depth == 0)
                    {
                        GUI.DrawTexture(cellRect, s_TestIcons[0], ScaleMode.ScaleToFit);
                    }
                    // cellRect.xMin += 2;
                    // cellRect.yMin += 2;
                    // cellRect.xMax -= 2;
                    // cellRect.xMax -= 2;
                    // if (GUI.Button(cellRect, ""))
                    // {
                    //     Debug.Log("delete?");
                    // }
                    // GUI.DrawTexture(cellRect, kIconDelete, ScaleMode.ScaleToFit);
                    break;
                case 1:
                    if (item.depth == 1)
                    {
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.target != null)
                        {
                            if (target.target is GameObject)
                            {

                            }
                            else
                            {
                                var assetPath = AssetDatabase.GetAssetPath(target.target);
                                if (Directory.Exists(assetPath))
                                {
                                    GUI.DrawTexture(cellRect, s_TestIcons[1], ScaleMode.ScaleToFit);
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
                    if (item.depth == 1)
                    {
                        var assetTarget = (item as BundleBuilderTreeViewTarget).assetTarget;
                        cellRect.xMin += indent + kToggleWidth + 2f;
                        var target = EditorGUI.ObjectField(cellRect, GUIContent.none, assetTarget.target, typeof(Object), false);
                        if (target != assetTarget.target)
                        {
                            assetTarget.target = target;
                            EditorUtility.SetDirty(_data);
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
                        var type = (BundleType)EditorGUI.EnumPopup(cellRect, bundleInfo.type);
                        if (type != bundleInfo.type)
                        {
                            bundleInfo.type = type;
                            EditorUtility.SetDirty(_data);
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
                                    EditorUtility.SetDirty(_data);
                                }
                            }
                        }
                    }
                    break;
                case 4:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var load = (BundleLoad)EditorGUI.EnumPopup(cellRect, bundleInfo.load);
                        if (load != bundleInfo.load)
                        {
                            bundleInfo.load = load;
                            EditorUtility.SetDirty(_data);
                        }
                    }
                    else if (item.depth == 1)
                    {
                        // var bundleInfo = (item.parent as BundleBuilderTreeViewBundle).bundleInfo;
                        var target = (item as BundleBuilderTreeViewTarget).assetTarget;
                        if (target.target != null)
                        {
                            var platforms = (BundleAssetPlatforms)EditorGUI.EnumFlagsField(cellRect, target.platforms);
                            if (platforms != target.platforms)
                            {
                                target.platforms = platforms;
                                EditorUtility.SetDirty(_data);
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
                            EditorUtility.SetDirty(_data);
                        }
                    }
                    break;
                case 6:
                    if (item.depth == 0)
                    {
                        var bundleInfo = (item as BundleBuilderTreeViewBundle).bundleInfo;
                        var splitObjects = EditorGUI.IntSlider(cellRect, bundleInfo.splitObjects, 0, 100);
                        if (splitObjects != bundleInfo.splitObjects)
                        {
                            bundleInfo.splitObjects = splitObjects;
                            EditorUtility.SetDirty(_data);
                        }
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
