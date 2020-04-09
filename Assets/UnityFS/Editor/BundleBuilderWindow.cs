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
            _searchKeyword = EditorPrefs.GetString("BundleBuilderWindow._searchKeyword");
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
                case 0: OnDrawPackages();
                    break;
                case 1: OnDrawAssets();
                    break;
                case 2: OnDrawSettings();
                    break;
            }
        }

        private Vector2 _searchSV;
        private string _searchKeyword;
        
        private void OnDrawAssets()
        {
            var showDefinedOnly = EditorPrefs.GetInt("BundleBuilderWindow.showDefinedOnly") == 1;
            Block("Search", () =>
            {
                var nSearchKeyword = EditorGUILayout.TextField("Keyword", _searchKeyword);
                if (nSearchKeyword != _searchKeyword)
                {
                    _searchKeyword = nSearchKeyword;
                    EditorPrefs.SetString("BundleBuilderWindow._searchKeyword", _searchKeyword);
                }
                var nShowDefinedOnly = EditorGUILayout.Toggle("Show Defined Only", showDefinedOnly);
                if (nShowDefinedOnly != showDefinedOnly)
                {
                    showDefinedOnly = nShowDefinedOnly;
                    EditorPrefs.SetInt("BundleBuilderWindow.showDefinedOnly", showDefinedOnly ? 1 : 0);
                }
            });
            
            EditorGUILayout.Space();
            Block("Results", () =>
            {
                _searchSV = EditorGUILayout.BeginScrollView(_searchSV);
                
                if (true)
                {
                    var searchCount = 200;
                    for (var i = 0; i < data.allCollectedAssetsPath.Length; i++)
                    {
                        var assetPath = data.allCollectedAssetsPath[i];
                        if (string.IsNullOrEmpty(_searchKeyword) || assetPath.IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                            AssetAttributes attrs;
                            if (data.assetAttributesMap.TryGetValue(assetGuid, out attrs))
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.TextField(assetPath);
                                EditorGUILayout.ObjectField(assetObject, typeof(Object), false, GUILayout.Width(200f));
                                EditorGUI.BeginChangeCheck();
                                attrs.priority = EditorGUILayout.IntSlider(attrs.priority, 0, data.priorityMax);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    data.MarkAsDirty();
                                }
                                GUI.color = Color.red;
                                if (GUILayout.Button("Del", GUILayout.Width(60f)))
                                {
                                    if (EditorUtility.DisplayDialog("删除", $"确定删除资源项属性?", "确定", "取消"))
                                    {
                                        Defer(() =>
                                        {
                                            data.assetAttributesMap.Remove(assetGuid);
                                            data.MarkAsDirty();
                                        });
                                    }
                                }

                                GUI.color = _GUIColor;
                                EditorGUILayout.EndHorizontal();
                                --searchCount;
                            }
                            else if (!showDefinedOnly)
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.TextField(assetPath);
                                EditorGUILayout.ObjectField(assetObject, typeof(Object), false, GUILayout.Width(200f));
                                var nSlideValue = EditorGUILayout.IntSlider(0, 0, data.priorityMax);
                                if (nSlideValue != 0)
                                {
                                    AddAssetAttribute(assetGuid, nSlideValue);
                                }
                                if (GUILayout.Button("Add", GUILayout.Width(60f)))
                                {
                                    AddAssetAttribute(assetGuid, 0);
                                }
                                EditorGUILayout.EndHorizontal();
                                --searchCount;
                            }

                            if (searchCount <= 0)
                            {
                                break;
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            });
            EditorGUILayout.Space();
        }

        private void AddAssetAttribute(string assetGuid, int priority)
        {
            data.assetAttributesMap[assetGuid] = new AssetAttributes()
            {
                priority = priority
            };
            data.MarkAsDirty();
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
                    BundleBuilder.Scan(data, data.previewPlatform);
                    _treeView.Reload();
                }

                if (GUILayout.Button("Show Bundle Assets"))
                {
                    BundleBuilder.Scan(data, data.previewPlatform);
                    _treeView.ShowBundleReport();
                }

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