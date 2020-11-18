using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleAssetsWindow : BaseEditorWindow
    {
        private BundleBuilderData _data;
        private IList<BundleBuilderData.BundleInfo> _bundles;
        private Vector2 _sv;

        protected override void OnEnable()
        {
            titleContent = new GUIContent("Bundle Editor");
        }

        public static void Inspect(BundleBuilderData data, IList<BundleBuilderData.BundleInfo> bundles)
        {
            var win = EditorWindow.GetWindow<BundleAssetsWindow>();
            win.SetBundles(data, bundles);
            win.Show();
        }

        public void SetBundles(BundleBuilderData data, IList<BundleBuilderData.BundleInfo> bundles)
        {
            _data = data;
            _bundles = bundles;
        }

        protected override void OnGUIDraw()
        {
            _foldoutStyle.normal.textColor = GUI.skin.button.normal.textColor;
            _GUIColor = GUI.color;
            if (_bundles == null || _bundles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }

            GUILayout.Space(4f);

            _sv = GUILayout.BeginScrollView(_sv);
            foreach (var bundle in _bundles)
            {
                InspectBundle(bundle);
            }

            GUILayout.EndScrollView();
            ExecuteDefers();
        }

        private void InspectRules(IList<BundleBuilderData.BundleSplitRule> rules)
        {
            Block("Rules", () =>
            {
                var rulesCount = rules.Count;
                if (rulesCount > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Asset Types", GUILayout.Width(80f));
                    EditorGUILayout.LabelField("Match Type", GUILayout.Width(80f));
                    EditorGUILayout.LabelField("Keyword", GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                    EditorGUILayout.LabelField("Exclude?", GUILayout.Width(50f));
                    GUILayout.Space(20f);
                    EditorGUILayout.EndHorizontal();
                }

                for (var i = 0; i < rulesCount; i++)
                {
                    var rule = rules[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    rule.assetTypes =
                        (BundleAssetTypes)EditorGUILayout.EnumFlagsField(rule.assetTypes, GUILayout.Width(80f));
                    rule.type = (BundleBuilderData.BundleSplitType)EditorGUILayout.EnumPopup(rule.type,
                        GUILayout.Width(80f));
                    rule.keyword = EditorGUILayout.TextField(rule.keyword, GUILayout.MinWidth(80f),
                        GUILayout.ExpandWidth(true));
                    rule.exclude = EditorGUILayout.Toggle(rule.exclude, GUILayout.Width(50f));
                    EditorGUI.BeginDisabledGroup(rule.exclude);
                    EditorGUI.EndDisabledGroup();
                    if (EditorGUI.EndChangeCheck())
                    {
                        _data.MarkAsDirty();
                    }

                    GUI.color = Color.red;
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                    rect.y -= 2f;
                    rect.height += 1f;
                    if (GUI.Button(rect, Text("delete.rule", "X", "删除规则")))
                    {
                        if (EditorUtility.DisplayDialog("删除", $"确定删除规则?", "确定", "取消"))
                        {
                            Defer(() =>
                            {
                                rules.Remove(rule);
                                _data.MarkAsDirty();
                            });
                        }
                    }

                    GUI.color = _GUIColor;
                    EditorGUILayout.EndHorizontal();
                }
            }, () =>
            {
                GUI.color = Color.green;
                var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                rect.y -= 2f;
                rect.height += 1f;
                if (GUI.Button(rect, Text("add.rule", "+", "添加分包分片规则")))
                {
                    Defer(() =>
                    {
                        rules.Add(new BundleBuilderData.BundleSplitRule());
                        _data.MarkAsDirty();
                    });
                }

                GUI.color = _GUIColor;
            });
        }

        private bool IsDuplicated(BundleBuilderData.BundleInfo bundle, BundleBuilderData.BundleSplit target)
        {
            for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
            {
                var split = bundle.splits[splitIndex];
                if (split != target && target.name == split.name)
                {
                    return true;
                }
            }

            return false;
        }

        private void InspectBundle(BundleBuilderData.BundleInfo bundle)
        {
            var bundleName = string.IsNullOrEmpty(bundle.name) ? "(null)" : bundle.name;
            Block(bundleName, () =>
            {
                Block("Basic", () =>
                {
                    EditorGUI.BeginChangeCheck();
                    bundle.note = EditorGUILayout.TextField("Info", bundle.note);
                    bundle.tag = EditorGUILayout.TextField("Tag", bundle.tag);
                    bundle.streamingAssets = EditorGUILayout.Toggle("StreamingAssets", bundle.streamingAssets);
                    bundle.load = (Manifest.BundleLoad)EditorGUILayout.EnumPopup("Load", bundle.load);
                    bundle.type = (Manifest.BundleType)EditorGUILayout.EnumPopup("Type", bundle.type);
                    bundle.priority = EditorGUILayout.IntSlider("Priority", bundle.priority, 0, 10000);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _data.MarkAsDirty();
                    }
                });

                Block("Target Assets", () =>
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(44f);
                    var addObject = EditorGUILayout.ObjectField(null, typeof(Object), false);
                    if (addObject != null)
                    {
                        Defer(() =>
                        {
                            var addObjectPath = AssetDatabase.GetAssetPath(addObject);
                            bundle.targets.Add(new BundleBuilderData.BundleAssetTarget()
                            {
                                enabled = true,
                                targetPath = addObjectPath,
                            });
                        });
                    }

                    EditorGUILayout.EndHorizontal();

                    var size = bundle.targets.Count;
                    for (var i = 0; i < size; i++)
                    {
                        var target = bundle.targets[i];
                        EditorGUILayout.BeginHorizontal();
                        GUI.color = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(20f)))
                        {
                            if (EditorUtility.DisplayDialog("删除", $"确定删除资源项?", "确定", "取消"))
                            {
                                Defer(() => bundle.targets.Remove(target));
                            }
                        }

                        GUI.color = _GUIColor;
                        EditorGUI.BeginChangeCheck();
                        target.enabled = EditorGUILayout.Toggle(target.enabled, GUILayout.Width(12f));
                        if (target.targetPath.StartsWith("Assets/"))
                        {
                            var targetAsset = AssetDatabase.LoadMainAssetAtPath(target.targetPath);
                            EditorGUILayout.ObjectField(targetAsset, typeof(Object), false);
                        }
                        else
                        {
                            EditorGUILayout.TextField(target.targetPath);
                        }
                        target.platform = (PackagePlatform)EditorGUILayout.EnumPopup(target.platform);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _data.MarkAsDirty();
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                });

                Block("Bundle Splits", () =>
                {
                    for (int splitIndex = 0, splitCount = bundle.splits.Count; splitIndex < splitCount; splitIndex++)
                    {
                        var bundleSplit = bundle.splits[splitIndex];
                        var splitName = string.IsNullOrEmpty(bundleSplit.name) ? "(default)" : bundleSplit.name;
                        Foldout(splitName, () =>
                        {
                            var sliceCount = bundleSplit.slices.Count;
                            EditorGUI.BeginChangeCheck();
                            var duplicated = IsDuplicated(bundle, bundleSplit);
                            if (duplicated)
                            {
                                GUI.color = Color.yellow;
                                bundleSplit.name = EditorGUILayout.TextField(
                                    Text("bundle.split.name", "Name", "warning: duplicated bundle split name"),
                                    bundleSplit.name);
                                GUI.color = _GUIColor;
                            }
                            else
                            {
                                bundleSplit.name = EditorGUILayout.TextField("Name", bundleSplit.name);
                            }

                            bundleSplit.encrypted = EditorGUILayout.Toggle("Encrypted?", bundleSplit.encrypted);
                            bundleSplit.sliceObjects = EditorGUILayout.IntField("Slice Objects", bundleSplit.sliceObjects);
                            var bundleSplitRawSize = 0L;
                            var bundleSplitBuildSize = 0L;
                            bundleSplit.GetTotalSize(out bundleSplitRawSize, out bundleSplitBuildSize);
                            EditorGUILayout.LabelField("Total (Raw)", PathUtils.GetFileSizeString(bundleSplitRawSize));
                            EditorGUILayout.LabelField("Total (Build)", PathUtils.GetFileSizeString(bundleSplitBuildSize));
                            if (EditorGUI.EndChangeCheck())
                            {
                                _data.MarkAsDirty();
                            }

                            InspectRules(bundleSplit.rules);

                            var validIndex = 0;
                            for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
                            {
                                var bundleSlice = bundleSplit.slices[sliceIndex];
                                var assetCount = bundleSlice.GetAssetCount();
                                if (assetCount > 0)
                                {
                                    validIndex++;
                                    Block("Slices", () =>
                                    {
                                        var sliceName = bundleSlice.name;
                                        if (sliceCount > 1)
                                        {
                                            sliceName = string.Format("[{0}] {1}/{2}: {3}", validIndex,
                                                sliceIndex + 1, sliceCount,
                                                sliceName);
                                        }

                                        if (bundleSlice.streamingAssets)
                                        {
                                            GUI.color = Color.green;
                                        }

                                        EditorGUILayout.LabelField(sliceName);
                                        var intent = 40f;
                                        EditorGUILayout.BeginHorizontal();
                                        GUILayout.Space(intent);
                                        EditorGUILayout.BeginVertical();
                                        EditorGUI.BeginDisabledGroup(true);
                                        // var nStreamingAssets =
                                        EditorGUILayout.Toggle("StreamingAssets", bundleSlice.streamingAssets);
                                        // if (nStreamingAssets != slice.streamingAssets)
                                        // {
                                        //     slice.streamingAssets = nStreamingAssets;
                                        //     _data.MarkAsDirty();
                                        // }
                                        EditorGUILayout.LabelField("Total (Raw): ", PathUtils.GetFileSizeString(bundleSlice.totalRawSize));
                                        EditorGUILayout.LabelField("Total (Build): ", PathUtils.GetFileSizeString(bundleSlice.lastBuildSize));
                                        EditorGUILayout.IntField("Objects: ", assetCount);
                                        EditorGUILayout.EnumPopup("Platform", bundleSlice.platform);
                                        EditorGUI.EndDisabledGroup();

                                        if (_data.showBundleDetails)
                                        {
                                            //TODO: 太卡了, 需要优化展示方式
                                            for (var assetIndex = 0; assetIndex < assetCount; assetIndex++)
                                            {
                                                var assetPath = bundleSlice.GetAssetPath(assetIndex);
                                                EditorGUILayout.BeginHorizontal();
                                                DrawSingleAssetAttributes(_data, assetPath);
                                                if (GUILayout.Button("?", GUILayout.Width(20f)))
                                                {
                                                    BundleBuilderWindow.DisplayAssetAttributes(assetPath);
                                                }

                                                EditorGUILayout.EndHorizontal();
                                            }
                                        }

                                        EditorGUILayout.EndVertical();
                                        EditorGUILayout.EndHorizontal();
                                        GUI.color = _GUIColor;
                                    }, () =>
                                    {
                                        GUI.color = Color.magenta;
                                        var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                                        rect.y -= 2f;
                                        rect.height += 1f;
                                        if (GUI.Button(rect, Text("reconstruct.split.slice", "❃", "重构分包切分")))
                                        {
                                            if (EditorUtility.DisplayDialog("重构", $"确定重构分包切分?", "确定", "取消"))
                                            {
                                                Defer(() =>
                                                {
                                                    bundleSlice.Reset();
                                                    BundleBuilder.Scan(_data);
                                                    _data.MarkAsDirty();
                                                });
                                            }
                                        }

                                        GUI.color = _GUIColor;
                                    }, () =>
                                    {
                                        GUI.color = Color.red;
                                        var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                                        rect.y -= 2f;
                                        rect.height += 1f;
                                        if (GUI.Button(rect, Text("delete.split.slice", "X", "删除分包切分")))
                                        {
                                            if (EditorUtility.DisplayDialog("删除", $"确定删除分包切分?", "确定", "取消"))
                                            {
                                                Defer(() =>
                                                {
                                                    bundleSplit.slices.Remove(bundleSlice);
                                                    BundleBuilder.Scan(_data);
                                                    _data.MarkAsDirty();
                                                });
                                            }
                                        }

                                        GUI.color = _GUIColor;
                                    });
                                }
                            }
                        }, () =>
                        {
                            GUI.color = Color.yellow;
                            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                            rect.y -= 2f;
                            rect.height += 1f;
                            EditorGUI.BeginDisabledGroup(splitIndex == 0);
                            if (GUI.Button(rect, Text("moveup.split", "▲", "向前移动")))
                            {
                                var newSplitIndex = splitIndex - 1;
                                Defer(() =>
                                {
                                    bundle.splits.Remove(bundleSplit);
                                    bundle.splits.Insert(newSplitIndex, bundleSplit);
                                    _data.MarkAsDirty();
                                });
                            }

                            EditorGUI.EndDisabledGroup();
                            GUI.color = _GUIColor;
                        }, () =>
                        {
                            GUI.color = Color.yellow;
                            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                            rect.y -= 2f;
                            rect.height += 1f;
                            EditorGUI.BeginDisabledGroup(splitIndex == splitCount - 1);
                            if (GUI.Button(rect, Text("movedown.split", "▼", "向后移动")))
                            {
                                var newSplitIndex = splitIndex + 1;
                                Defer(() =>
                                {
                                    bundle.splits.Remove(bundleSplit);
                                    bundle.splits.Insert(newSplitIndex, bundleSplit);
                                    _data.MarkAsDirty();
                                });
                            }

                            EditorGUI.EndDisabledGroup();
                            GUI.color = _GUIColor;
                        }, () =>
                        {
                            GUI.color = Color.magenta;
                            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                            rect.y -= 2f;
                            rect.height += 1f;
                            if (GUI.Button(rect, Text("reconstruct.split", "❃", "重构分包")))
                            {
                                if (EditorUtility.DisplayDialog("重构", $"确定重构分包?", "确定", "取消"))
                                {
                                    Defer(() =>
                                    {
                                        bundleSplit.Reset();
                                        BundleBuilder.Scan(_data);
                                        _data.MarkAsDirty();
                                    });
                                }
                            }

                            GUI.color = _GUIColor;
                        }, () =>
                        {
                            GUI.color = Color.red;
                            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                            rect.y -= 2f;
                            rect.height += 1f;
                            if (GUI.Button(rect, Text("delete.split", "X", "删除分包")))
                            {
                                if (EditorUtility.DisplayDialog("删除", $"确定删除分包?", "确定", "取消"))
                                {
                                    Defer(() =>
                                    {
                                        bundle.splits.Remove(bundleSplit);
                                        _data.MarkAsDirty();
                                    });
                                }
                            }

                            GUI.color = _GUIColor;
                        });
                    }
                }, () =>
                {
                    GUI.color = Color.green;
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(20f));
                    rect.y -= 2f;
                    rect.height += 1f;
                    if (GUI.Button(rect, Text("add.split", "+", "添加分包")))
                    {
                        Defer(() =>
                        {
                            var newSplit = new BundleBuilderData.BundleSplit();
                            bundle.splits.Add(newSplit);
                            _data.MarkAsDirty();
                        });
                    }

                    GUI.color = _GUIColor;
                });
            });
        }

        private static AssetAttributes DrawSingleAssetAttributes(BundleBuilderData data, string assetPath)
        {
            var fileInfoWidth = 60f;
            var fileInfo = new FileInfo(assetPath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0L;
            var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var attrs = data.GetAssetPathAttributes(assetPath);
            var bNew = attrs == null;

            if (bNew)
            {
                attrs = new AssetAttributes();
            }

            var nAssetPacker = (AssetPacker)EditorGUILayout.EnumPopup(attrs.packer, GUILayout.MaxWidth(110f));
            var nPriority = EditorGUILayout.IntSlider(attrs.priority, 0, data.priorityMax, GUILayout.MaxWidth(220f));
            EditorGUILayout.ObjectField(assetObject, typeof(Object), false, GUILayout.MaxWidth(180f));
            EditorGUILayout.TextField(assetPath);
            EditorGUILayout.LabelField(PathUtils.GetFileSizeString(fileSize), _rightAlignStyle, GUILayout.MaxWidth(fileInfoWidth));

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

            return attrs;
        }
    }
}