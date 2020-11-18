using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public partial class BundleBuilderWindow : BaseEditorWindow
    {
        public const string KeyForPackagePlatforms = ".BundleBuilderWindow.Platforms";
        public const string KeyForTabIndex = ".BundleBuilderWindow.TabIndex";
        public const string KeyForSearchKey = "BundleBuilderWindow._searchKeyword";
        public const string KeyForSearchSliceKey = "BundleBuilderWindow._searchSliceKeyword";
        public const string KeyForShowDefinedOnly = "BundleBuilderWindow.showDefinedOnly";
        public const string KeyForUseRegexMatch = "BundleBuilderWindow.UseRegexMatch";
        public const string KeyForShowSelectionOnly = "BundleBuilderWindow.showSelectionOnly";
        public const string KeyForShowStreamingAssetsOnly = "BundleBuilderWindow.showStreamingAssetsOnly";
        [SerializeField] MultiColumnHeaderState _headerState;
        [SerializeField] TreeViewState _treeViewState = new TreeViewState();
        BundleBuilderTreeView _treeView;

        private int _tabIndex;
        private string[] _tabs = new[] { "Packages", "Assets", "Settings" };
        private BundleBuilderData _data;
        private PackagePlatform _platform;

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
                var filePath = "Assets/unityfs_asset_list" + (index > 0 ? "_" + (index++) : "") + Manifest.AssetListDataExt;
                if (!File.Exists(filePath))
                {
                    var listData = new AssetListData();
                    AssetListData.WriteTo(filePath, listData);
                    AssetDatabase.Refresh();
                    return;
                }
            } while (true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Selection.selectionChanged -= OnSelectionChanged;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Selection.selectionChanged += OnSelectionChanged;
            _data = BundleBuilder.GetData();
            BundleBuilder.Scan(_data);
            titleContent = new GUIContent("Bundle Builder");
            _searchKeyword = EditorPrefs.GetString(KeyForSearchKey);
            _searchSliceKeyword = EditorPrefs.GetString(KeyForSearchSliceKey);
            _showDefinedOnly = EditorPrefs.GetInt(KeyForShowDefinedOnly) == 1;
            _useRegexMatch = EditorPrefs.GetInt(KeyForUseRegexMatch) == 1;
            _showSelectionOnly = EditorPrefs.GetInt(KeyForShowSelectionOnly) == 1;
            _showStreamingAssetsOnly = EditorPrefs.GetInt(KeyForShowStreamingAssetsOnly) == 1;
            UpdateSearchResults();
            _tabIndex = EditorPrefs.GetInt(KeyForTabIndex);
            _platform = (PackagePlatform)EditorPrefs.GetInt(KeyForPackagePlatforms, (int)PackagePlatform.Any);
            // bool firstInit = _headerState == null;
            var headerState = BundleBuilderTreeView.CreateDefaultMultiColumnHeaderState(this.position.width);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(_headerState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(_headerState, headerState);
            }

            var header = new BundleBuilderTreeViewHeader(headerState);
            _headerState = headerState;

            _treeView = new BundleBuilderTreeView(_treeViewState, header);
            _treeView.SetData(_data);
        }

        private void OnSelectionChanged()
        {
            if (_showSelectionOnly)
            {
                UpdateSearchResults();
                Repaint();
            }
        }

        public static void DisplayAssetAttributes(string assetPath)
        {
            var window = GetWindow<BundleBuilderWindow>();
            window._tabIndex = 1;
            window._searchKeyword = assetPath;
            window._searchSliceKeyword = "";
            window._showDefinedOnly = false;
            window._useRegexMatch = false;
            window._showSelectionOnly = false;
            EditorPrefs.SetString(KeyForSearchKey, window._searchKeyword);
            EditorPrefs.SetString(KeyForSearchSliceKey, window._searchSliceKeyword);
            EditorPrefs.SetInt(KeyForShowDefinedOnly, window._showDefinedOnly ? 1 : 0);
            EditorPrefs.SetInt(KeyForUseRegexMatch, window._useRegexMatch ? 1 : 0);
            EditorPrefs.SetInt(KeyForShowSelectionOnly, window._showSelectionOnly ? 1 : 0);
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

        private SVirtualScrollInfo _searchSV;
        private bool _showStreamingAssetsOnly; // 仅列出 StreamingAssets 资源
        private bool _showDefinedOnly; // 仅列出非默认的 (有修改)
        private bool _showSelectionOnly; // 仅列出 Project 窗口中选中的
        private bool _useRegexMatch; // 是否启用正则匹配
        private string _searchKeyword;  // 仅列出符合指定关键字的资源
        private string _searchSliceKeyword; // 仅列出符合指定关键字的包中的资源 
        private bool _batchedSelectMarks;
        public static AssetAttributes _newAttrs = new AssetAttributes();
        private HashSet<SearchResult> _searchMarks = new HashSet<SearchResult>();
        private List<SearchResult> _searchResults = new List<SearchResult>();

        // showDefinedOnly: 只显示已定义
        // searchCount: 结果数量限制
        private void UpdateSearchResults()
        {
            var keyword = _searchKeyword;
            var sliceKeyword = _searchSliceKeyword;
            var showDefinedOnly = _showDefinedOnly;
            var useRegexMatch = _useRegexMatch;
            var showSelectionOnly = _showSelectionOnly;
            var showStreamingAssetsOnly = _showStreamingAssetsOnly;
            // var searchCount = _data.searchMax;

            Regex nameRegex = null;
            Regex sliceNameRegex = null;

            if (_useRegexMatch)
            {
                nameRegex = MakeRegex(keyword);
                sliceNameRegex = MakeRegex(sliceKeyword);
            }

            _searchResults.Clear();
            var selectionSet = new HashSet<string>();
            if (showSelectionOnly)
            {
                for (int i = 0, size = Selection.objects.Length; i < size; i++)
                {
                    var sel = Selection.objects[i];
                    var assetPath = AssetDatabase.GetAssetPath(sel);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        selectionSet.Add(assetPath);
                    }
                }
            }

            _data.ForEachAssetPath((bundleInfo, bundleSplit, bundleSlice, assetPath) =>
            {
                if (!showStreamingAssetsOnly || bundleSlice.streamingAssets)
                {
                    if (!showSelectionOnly || selectionSet.Contains(assetPath))
                    {
                        if (IsStringMatch(nameRegex, keyword, assetPath))
                        {
                            if (IsStringMatch(sliceNameRegex, sliceKeyword, bundleSlice.name))
                            {
                                var attrs = _data.GetAssetPathAttributes(assetPath);
                                if (attrs != null || !showDefinedOnly)
                                {
                                    var result = new SearchResult()
                                    {
                                        bundleInfo = bundleInfo,
                                        bundleSplit = bundleSplit,
                                        bundleSlice = bundleSlice,
                                        assetPath = assetPath,
                                    };

                                    _searchResults.Add(result);
                                }
                            }
                        }
                    }
                }
            });
        }

        private Regex MakeRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return null;
            }

            try
            {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Singleline);
                return regex;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool IsStringMatch(Regex regex, string keyword, string text)
        {
            if (regex != null)
            {
                return regex.IsMatch(text);
            }

            return string.IsNullOrEmpty(keyword) || text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplyAllMarks(Action<AssetAttributes> callback)
        {
            foreach (var searchMark in _searchMarks)
            {
                if (_searchResults.Contains(searchMark))
                {
                    var markAttrs = _data.GetAssetPathAttributes(searchMark.assetPath);
                    var bNew = markAttrs == null;

                    callback(bNew ? _newAttrs : markAttrs);
                    if (bNew)
                    {
                        if (_newAttrs.priority != 0 || _newAttrs.packer != AssetPacker.Auto)
                        {
                            var newAttributes = _data.AddAssetPathAttributes(searchMark.assetPath);
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
                            _data.RemoveAssetPathAttributes(searchMark.assetPath);
                        }
                    }
                }
            }
        }

        private static AssetAttributes DrawSearchResultAssetAttributes(Rect elementRect, BundleBuilderData data, SearchResult result, BundleBuilderWindow builder, bool batchMode)
        {
            var assetPath = result.assetPath;
            var fileInfoWidth = 60f;
            var sliceInfoWidth = 260f;
            var fileInfo = new FileInfo(assetPath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0L;
            var attrs = data.GetAssetPathAttributes(assetPath);
            var bNew = attrs == null;

            if (bNew)
            {
                attrs = new AssetAttributes();
            }

            var iRect = new Rect(elementRect.x, elementRect.y, 110f, elementRect.height);
            var nAssetPacker = (AssetPacker)EditorGUI.EnumPopup(iRect, attrs.packer);
            iRect.x += 110f + 8f;
            iRect.width = 220f;
            iRect.height = elementRect.height - 2f;
            var nPriority = EditorGUI.IntSlider(iRect, attrs.priority, 0, data.priorityMax);
            iRect.x += iRect.width;
            iRect.width = 180f;
            iRect.height = elementRect.height - 4f;
            if (assetPath.StartsWith("Assets/"))
            {
                var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                EditorGUI.ObjectField(iRect, assetObject, typeof(Object), false);
            }
            else
            {
                EditorGUI.LabelField(iRect, "<External>");
            }
            iRect.x += iRect.width;
            iRect.width = fileInfoWidth;
            iRect.height = elementRect.height - 2f;
            EditorGUI.LabelField(iRect, PathUtils.GetFileSizeString(fileSize), _rightAlignStyle);

            iRect.x += iRect.width;
            iRect.width = elementRect.width - iRect.x - sliceInfoWidth - 20f + 20f;
            EditorGUI.TextField(iRect, assetPath);

            iRect.x += iRect.width;
            iRect.width = sliceInfoWidth;
            iRect.height = elementRect.height - 2f;
            if (result.bundleInfo != null)
            {
                EditorGUI.TextField(iRect, result.bundleSlice.name);
                iRect.x += iRect.width;
                iRect.width = 20f;
                if (GUI.Button(iRect, ">"))
                {
                    BundleAssetsWindow.Inspect(data, new List<BundleBuilderData.BundleInfo>(new[] { result.bundleInfo }));
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(iRect, "<null>");
                iRect.x += iRect.width;
                iRect.width = 20f;
                GUI.Button(iRect, ">");
                EditorGUI.EndDisabledGroup();
            }

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
                    data.RemoveAssetPathAttributes(assetPath);
                }
                else if (bNew)
                {
                    if (attrs.priority != 0 || attrs.packer != AssetPacker.Auto)
                    {
                        var newAttributes = data.AddAssetPathAttributes(assetPath);
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

                var nSearchSliceKeyword = EditorGUILayout.TextField("Bundle Slice Keyword", _searchSliceKeyword);
                if (nSearchSliceKeyword != _searchSliceKeyword)
                {
                    _searchSliceKeyword = nSearchSliceKeyword;
                    EditorPrefs.SetString(KeyForSearchSliceKey, _searchSliceKeyword);
                    UpdateSearchResults();
                }

                var nShowDefinedOnly = EditorGUILayout.Toggle("Show Defined Only", _showDefinedOnly);
                if (nShowDefinedOnly != _showDefinedOnly)
                {
                    _showDefinedOnly = nShowDefinedOnly;
                    EditorPrefs.SetInt(KeyForShowDefinedOnly, _showDefinedOnly ? 1 : 0);
                    UpdateSearchResults();
                }

                var nUseRegexMatch = EditorGUILayout.Toggle("Use Regex Match", _useRegexMatch);
                if (nUseRegexMatch != _useRegexMatch)
                {
                    _useRegexMatch = nUseRegexMatch;
                    EditorPrefs.SetInt(KeyForUseRegexMatch, _useRegexMatch ? 1 : 0);
                    UpdateSearchResults();
                }

                var nShowSelectionOnly = EditorGUILayout.Toggle("Show Selection Only", _showSelectionOnly);
                if (nShowSelectionOnly != _showSelectionOnly)
                {
                    _showSelectionOnly = nShowSelectionOnly;
                    EditorPrefs.SetInt(KeyForShowSelectionOnly, _showSelectionOnly ? 1 : 0);
                    UpdateSearchResults();
                }

                var nShowStreamingAssetsOnly = EditorGUILayout.Toggle("Show StreamingAssets Only", _showStreamingAssetsOnly);
                if (nShowStreamingAssetsOnly != _showStreamingAssetsOnly)
                {
                    _showStreamingAssetsOnly = nShowStreamingAssetsOnly;
                    EditorPrefs.SetInt(KeyForShowStreamingAssetsOnly, _showStreamingAssetsOnly ? 1 : 0);
                    UpdateSearchResults();
                }
            });

            EditorGUILayout.Space();
            Block(string.Format("Results ({0}/{1})", _searchMarks.Count, _searchResults.Count), () =>
            {
                EditorGUILayout.BeginHorizontal();
                var nBatchedSelectMarks = EditorGUILayout.Toggle(_batchedSelectMarks, GUILayout.Width(20f));
                EditorGUILayout.LabelField("Asset Packer", GUILayout.Width(110f));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                if (nBatchedSelectMarks != _batchedSelectMarks)
                {
                    _batchedSelectMarks = nBatchedSelectMarks;
                    _searchMarks.Clear();
                    if (_batchedSelectMarks)
                    {
                        for (var i = 0; i < _searchResults.Count; i++)
                        {
                            _searchMarks.Add(_searchResults[i]);
                        }
                    }
                }

                var elementCount = _searchResults.Count;
                _searchSV.hint = -1;
                _searchSV = DrawVirtualScrollView(_searchSV, 22f, elementCount, (Rect elementRect, float elementHeight, int i) =>
                {
                    var result = _searchResults[i];
                    var marked = _searchMarks.Contains(result);
                    var headRect = new Rect(elementRect.x, elementRect.y, 20f, elementRect.height);
                    var nMarked = EditorGUI.Toggle(headRect, marked);
                    if (marked) // 批量修改模式
                    {
                        GUI.color = Color.green;
                    }

                    var lineRect = new Rect(elementRect.x + 20f, elementRect.y, elementRect.width - 20f, elementRect.height);
                    DrawSearchResultAssetAttributes(lineRect, _data, result, this, marked);
                    GUI.color = _GUIColor;

                    if (nMarked != marked)
                    {
                        if (nMarked)
                        {
                            _searchMarks.Add(result);
                        }
                        else
                        {
                            _searchMarks.Remove(result);
                        }
                    }
                });
            });
            EditorGUILayout.Space();
        }

        private string _newExts;

        private Vector2 _settingSV;
        private void OnDrawSettings()
        {
            _settingSV = EditorGUILayout.BeginScrollView(_settingSV);
            Block("Encryption", () =>
            {
                EditorGUI.BeginChangeCheck();
                _data.encryptionKey = EditorGUILayout.TextField("Password", _data.encryptionKey);
                _data.chunkSize = EditorGUILayout.IntField("Chunk Size", _data.chunkSize);
                var nChunkSize = Utils.ChunkedStream.GetChunkSize(_data.chunkSize);
                if (nChunkSize != _data.chunkSize)
                {
                    EditorGUILayout.HelpBox("Chunk Size: " + nChunkSize, MessageType.Info);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _data.MarkAsDirty();
                }
            });
            Block("Skip File Ext.", () =>
            {
                var count = _data.skipExts.Count;
                for (var i = 0; i < count; i++)
                {
                    var ext = _data.skipExts[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField(ext);
                    GUI.color = Color.red;
                    if (GUILayout.Button("X", GUILayout.Width(20f)))
                    {
                        var extV = ext;
                        Defer(() =>
                        {
                            _data.skipExts.Remove(extV);
                            _data.MarkAsDirty();
                        });
                    }

                    GUI.color = _GUIColor;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                _newExts = EditorGUILayout.TextField(_newExts);
                if (GUILayout.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(_newExts))
                    {
                        var exts = _newExts.Replace("\"", "").Replace(" ", "").Split(',');
                        _newExts = string.Empty;
                        Defer(() =>
                        {
                            foreach (var next in exts)
                            {
                                _data.skipExts.Add(next);
                            }

                            _data.MarkAsDirty();
                        });
                    }
                }

                EditorGUILayout.EndHorizontal();
            });
            Block("Misc.", () =>
            {
                EditorGUI.BeginChangeCheck();

                if (File.Exists(_data.mainAssetListPath))
                {
                    var assetH = AssetDatabase.LoadMainAssetAtPath(_data.mainAssetListPath);
                    var assetN = EditorGUILayout.ObjectField("资源访问分析", assetH, typeof(Object), false);
                    if (assetN != assetH)
                    {
                        _data.mainAssetListPath = AssetDatabase.GetAssetPath(assetN);
                    }
                }
                else
                {
                    var assetN = EditorGUILayout.ObjectField("资源访问分析", null, typeof(Object), false);
                    if (assetN != null)
                    {
                        _data.mainAssetListPath = AssetDatabase.GetAssetPath(assetN);
                    }
                }

                // 中间输出目录
                _data.assetBundlePath = EditorGUILayout.TextField("AssetBundle Path", _data.assetBundlePath);
                _data.zipArchivePath = EditorGUILayout.TextField("ZipArchive Path", _data.zipArchivePath);
                // 最终包输出目录
                _data.packagePath = EditorGUILayout.TextField("Package Path", _data.packagePath);
                _data.priorityMax = EditorGUILayout.IntField("Priority Max", _data.priorityMax);
                // _data.searchMax = EditorGUILayout.IntField("Search Max", _data.searchMax);
                _data.streamingAssetsAnyway = EditorGUILayout.Toggle("StreamingAssets Anyway", _data.streamingAssetsAnyway);
                _data.streamingAssetsManifest = EditorGUILayout.Toggle("StreamingAssets Manifest", _data.streamingAssetsManifest);
                _data.showBundleDetails = EditorGUILayout.Toggle("Show Bundle Details", _data.showBundleDetails);
                _data.disableTypeTree = EditorGUILayout.Toggle("Disable TypeTree", _data.disableTypeTree);
                _data.lz4Compression = EditorGUILayout.Toggle("LZ4 Compression", _data.lz4Compression);
                _data.deterministicAssetBundle = EditorGUILayout.Toggle("Deterministic AssetBundle", _data.deterministicAssetBundle);
#if !UNITY_2018_1_OR_NEWER
                EditorGUILayout.HelpBox("'extractShaderVariantCollections' require: UNITY_2018_1_OR_NEWER", MessageType.Warning);

#endif
                _data.extractShaderVariantCollections = EditorGUILayout.Toggle("Extract Shader Collections", _data.extractShaderVariantCollections);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Build", _data.build);
                EditorGUI.EndDisabledGroup();
                if (EditorGUI.EndChangeCheck())
                {
                    _data.MarkAsDirty();
                }
            });
            EditorGUILayout.EndScrollView();
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
                    _data.bundles.Add(new BundleBuilderData.BundleInfo()
                    {
                        id = ++_data.id,
                        name = $"bundle_{_data.id}{BundleBuilderData.FileExt}",
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
                GUILayout.Space(20f);
                if (GUILayout.Button("Reload"))
                {
                    Reload();
                }

                GUILayout.Space(20f);
                EditorGUILayout.LabelField("Targets", GUILayout.Width(46f));
                var platforms = (PackagePlatform)EditorGUILayout.EnumPopup(_platform, GUILayout.Width(90f));
                if (platforms != _platform)
                {
                    _platform = platforms;
                    EditorPrefs.SetInt(KeyForPackagePlatforms, (int)_platform);
                }

                if (GUILayout.Button("Build Packages"))
                {
                    BundleBuilder.BuildPackages(_data, "", _platform);
                }
            }

            GUILayout.EndArea();
        }

        private void Reload()
        {
            BundleBuilder.Scan(_data);
            _treeView.Reload();
        }
    }
}