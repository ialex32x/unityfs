using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderWindow : BaseEditorWindow
    {
        public const string KeyForPackagePlatforms = ".BundleBuilderWindow.Platforms";
        public const string KeyForTabIndex = ".BundleBuilderWindow.TabIndex";
        public const string KeyForSearchKey = "BundleBuilderWindow._searchKeyword";
        public const string KeyForShowDefinedOnly = "BundleBuilderWindow.showDefinedOnly";
        [SerializeField] MultiColumnHeaderState _headerState;
        [SerializeField] TreeViewState _treeViewState = new TreeViewState();
        BundleBuilderTreeView _treeView;

        private int _tabIndex;
        private string[] _tabs = new[] {"Packages", "Assets", "Settings"};
        private bool dirty;
        private BundleBuilderData.BundleInfo selected;
        private BundleBuilderData data;
        private PackagePlatforms _platforms;

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
            BundleBuilder.Scan(data, data.previewPlatform);
            titleContent = new GUIContent("Bundle Builder");
            _searchKeyword = EditorPrefs.GetString(KeyForSearchKey);
            _showDefinedOnly = EditorPrefs.GetInt(KeyForShowDefinedOnly) == 1;
            UpdateSearchResults();
            _tabIndex = EditorPrefs.GetInt(KeyForTabIndex);
            _platforms =
                (PackagePlatforms) EditorPrefs.GetInt(KeyForPackagePlatforms, (int) PackagePlatforms.Active);
            // bool firstInit = _headerState == null;
            var headerState = BundleBuilderTreeView.CreateDefaultMultiColumnHeaderState(this.position.width);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(_headerState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(_headerState, headerState);
            }

            var header = new BundleBuilderTreeViewHeader(headerState);
            _headerState = headerState;

            _treeView = new BundleBuilderTreeView(_treeViewState, header);
            _treeView.SetData(data);
        }

        public static void DisplayAssetAttributes(string guid)
        {
            var window = GetWindow<BundleBuilderWindow>();
            window._tabIndex = 1;
            window._searchKeyword = AssetDatabase.GUIDToAssetPath(guid);
            window._showDefinedOnly = false;
            EditorPrefs.SetString(KeyForSearchKey, window._searchKeyword);
            EditorPrefs.SetInt(KeyForShowDefinedOnly, window._showDefinedOnly ? 1 : 0);
            window.UpdateSearchResults();
        }

        protected override void OnGUIDraw()
        {
            var tabIndex = GUILayout.Toolbar(_tabIndex, _tabs);
            if (tabIndex != _tabIndex)
            {
                _tabIndex = tabIndex;
                EditorPrefs.SetInt(KeyForTabIndex, _tabIndex);
            }

            switch (tabIndex)
            {
                case 0:
                    OnDrawPackages();
                    break;
                case 1:
                    OnDrawAssets();
                    break;
                case 2:
                    OnDrawSettings();
                    break;
            }
        }

        private Vector2 _searchSV;
        private bool _showDefinedOnly;
        private string _searchKeyword;
        private bool _batchedSelectMarks;
        public static AssetAttributes _newAttrs = new AssetAttributes();
        private int _selectedInResults;
        private HashSet<string> _searchMarks = new HashSet<string>();
        private List<string> _searchResults = new List<string>();

        private void UpdateSearchResults()
        {
            UpdateSearchResults(_searchKeyword, _showDefinedOnly, 200);
        }

        // showDefinedOnly: 只显示已定义
        // searchCount: 结果数量限制
        private void UpdateSearchResults(string keyword, bool showDefinedOnly, int searchCount)
        {
            _searchResults.Clear();
            for (var i = 0; i < data.allCollectedAssetsPath.Length; i++)
            {
                var assetPath = data.allCollectedAssetsPath[i];
                if (string.IsNullOrEmpty(keyword) ||
                    assetPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    var attrs = data.GetAssetAttributes(assetGuid);
                    if (attrs != null || !showDefinedOnly)
                    {
                        _searchResults.Add(assetPath);
                    }

                    if (_searchResults.Count >= searchCount)
                    {
                        break;
                    }
                }
            }
        }

        private void ApplyAllMarks(Action<AssetAttributes> callback)
        {
            foreach (var searchMark in _searchMarks)
            {
                if (_searchResults.Contains(searchMark))
                {
                    var markGuid = AssetDatabase.AssetPathToGUID(searchMark);
                    var markAttrs = data.GetAssetAttributes(markGuid);
                    var bNew = markAttrs == null;

                    callback(bNew ? _newAttrs : markAttrs);
                    if (bNew)
                    {
                        if (_newAttrs.priority != 0 || _newAttrs.packer != AssetPacker.Auto)
                        {
                            var newAttributes = data.AddAssetAttributes(markGuid);
                            newAttributes.priority = _newAttrs.priority;
                            newAttributes.packer = _newAttrs.packer;
                            _newAttrs.priority = 0;
                            _newAttrs.packer = AssetPacker.Auto;
                        }
                    }
                    else
                    {
                        if (markAttrs.priority == 0 && markAttrs.packer == AssetPacker.Auto)
                        {
                            data.RemoveAssetAttributes(markGuid);
                        }
                    }
                }
            }
        }

        public static AssetAttributes DrawSingleAssetAttributes(BundleBuilderData data, string assetGuid)
        {
            return DrawSingleAssetAttributes(data, assetGuid, null, false);
        }

        private static AssetAttributes DrawSingleAssetAttributes(BundleBuilderData data, string assetGuid, BundleBuilderWindow builder, bool batchMode)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var attrs = data.GetAssetAttributes(assetGuid);
            var bNew = attrs == null;
            if (bNew)
            {
                attrs = new AssetAttributes();
            }
            var nAssetPacker =
                (AssetPacker) EditorGUILayout.EnumPopup(attrs.packer, GUILayout.MaxWidth(80f));
            var nPriority = EditorGUILayout.IntSlider(attrs.priority, 0, data.priorityMax,
                GUILayout.MaxWidth(220f));
            EditorGUILayout.ObjectField(assetObject, typeof(Object), false, GUILayout.MaxWidth(180f));
            EditorGUILayout.TextField(assetPath);

            if (batchMode)
            {
                if (nAssetPacker != attrs.packer)
                {
                    builder?.ApplyAllMarks(attributes => attributes.packer = nAssetPacker);
                }

                if (nPriority != attrs.priority)
                {
                    var deltaPriority = nPriority - attrs.priority;
                    builder?.ApplyAllMarks(attributes => attributes.priority = Math.Max(0,
                        Math.Min(data.priorityMax, attributes.priority + deltaPriority)));
                }
            }
            else
            {
                if (nAssetPacker != attrs.packer)
                {
                    attrs.packer = nAssetPacker;
                    data.MarkAsDirty();
                }

                if (nPriority != attrs.priority)
                {
                    attrs.priority = nPriority;
                    data.MarkAsDirty();
                }
                
                if (attrs.priority == 0 && attrs.packer == AssetPacker.Auto)
                {
                    data.RemoveAssetAttributes(assetGuid);
                }
                else if (bNew)
                {
                    if (attrs.priority != 0 || attrs.packer != AssetPacker.Auto)
                    {
                        var newAttributes = data.AddAssetAttributes(assetGuid);
                        newAttributes.priority = attrs.priority;
                        newAttributes.packer = attrs.packer;
                    }
                }
            }
            
            return attrs;
        }

        private void OnDrawAssets()
        {
            Block("Search", () =>
            {
                var nSearchKeyword = EditorGUILayout.TextField("Keyword", _searchKeyword);
                if (nSearchKeyword != _searchKeyword)
                {
                    _searchKeyword = nSearchKeyword;
                    EditorPrefs.SetString(KeyForSearchKey, _searchKeyword);
                    UpdateSearchResults();
                }

                var nShowDefinedOnly = EditorGUILayout.Toggle("Show Defined Only", _showDefinedOnly);
                if (nShowDefinedOnly != _showDefinedOnly)
                {
                    _showDefinedOnly = nShowDefinedOnly;
                    EditorPrefs.SetInt(KeyForShowDefinedOnly, _showDefinedOnly ? 1 : 0);
                    UpdateSearchResults();
                }
            });

            EditorGUILayout.Space();
            Block(string.Format("Results ({0}/{1})", _selectedInResults, _searchResults.Count), () =>
            {
                _selectedInResults = 0;
                var nbatchedSelectMarks = EditorGUILayout.Toggle(_batchedSelectMarks, GUILayout.Width(20f));
                if (nbatchedSelectMarks != _batchedSelectMarks)
                {
                    _batchedSelectMarks = nbatchedSelectMarks;
                    _searchMarks.Clear();
                    if (_batchedSelectMarks)
                    {
                        for (var i = 0; i < _searchResults.Count; i++)
                        {
                            _searchMarks.Add(_searchResults[i]);
                        }
                    }
                }

                _searchSV = EditorGUILayout.BeginScrollView(_searchSV);

                _batchedSelectMarks = false;
                for (var i = 0; i < _searchResults.Count; i++)
                {
                    var assetPath = _searchResults[i];
                    var marked = _searchMarks.Contains(assetPath);
                    var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    
                    EditorGUILayout.BeginHorizontal();
                    var nMarked = EditorGUILayout.Toggle(marked, GUILayout.Width(20f));
                    if (marked) // 批量修改模式
                    {
                        _selectedInResults++;
                        _batchedSelectMarks = true;
                        GUI.color = Color.green;
                    }

                    DrawSingleAssetAttributes(data, assetGuid, this, marked);
                    GUI.color = _GUIColor;
                    EditorGUILayout.EndHorizontal();

                    if (nMarked != marked)
                    {
                        if (nMarked)
                        {
                            _searchMarks.Add(assetPath);
                        }
                        else
                        {
                            _searchMarks.Remove(assetPath);
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            });
            EditorGUILayout.Space();
        }

        private void OnDrawSettings()
        {
            Block("Preview", () =>
            {
                EditorGUI.BeginChangeCheck();
                data.previewPlatform = (PackagePlatforms) EditorGUILayout.EnumPopup("Platform", data.previewPlatform);
                if (EditorGUI.EndChangeCheck())
                {
                    data.MarkAsDirty();
                }
            });
            Block("Encryption", () =>
            {
                EditorGUI.BeginChangeCheck();
                data.encryptionKey = EditorGUILayout.TextField("Password", data.encryptionKey);
                if (EditorGUI.EndChangeCheck())
                {
                    data.MarkAsDirty();
                }
            });
            Block("Path", () =>
            {
                EditorGUI.BeginChangeCheck();
                data.assetListData =
                    (AssetListData) EditorGUILayout.ObjectField("资源访问分析", data.assetListData, typeof(AssetListData),
                        false);
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
        }

        private void OnDrawPackages()
        {
            var margin = 5f;
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
                // if (GUILayout.Button("Collapse All"))
                // {
                //     _treeView.CollapseAll();
                // }
                //
                // if (GUILayout.Button("Expand All"))
                // {
                //     _treeView._ExpandAll();
                // }
                //
                GUILayout.Space(20f);
                if (GUILayout.Button("Reload"))
                {
                    BundleBuilder.Scan(data, data.previewPlatform);
                    _treeView.Reload();
                }
                // if (GUILayout.Button("Details"))
                // {
                //     BundleBuilder.Scan(data, data.previewPlatform);
                //     _treeView.ShowBundleReport();
                // }

                GUILayout.Space(20f);
                EditorGUILayout.LabelField("Targets", GUILayout.Width(46f));
                var platforms = (PackagePlatforms) EditorGUILayout.EnumFlagsField(_platforms, GUILayout.Width(90f));
                if (platforms != _platforms)
                {
                    _platforms = platforms;
                    EditorPrefs.SetInt(KeyForPackagePlatforms, (int) _platforms);
                }

                if (GUILayout.Button("Build Packages"))
                {
                    BundleBuilder.BuildPackages(data, "", _platforms);
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