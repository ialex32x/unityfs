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
                        (BundleAssetTypes) EditorGUILayout.EnumFlagsField(rule.assetTypes, GUILayout.Width(80f));
                    rule.type = (BundleBuilderData.BundleSplitType) EditorGUILayout.EnumPopup(rule.type,
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
                    bundle.streamingAssets = EditorGUILayout.Toggle("StreamingAssets", bundle.streamingAssets);
                    bundle.load = (Manifest.BundleLoad) EditorGUILayout.EnumPopup("Load", bundle.load);
                    bundle.type = (Manifest.BundleType) EditorGUILayout.EnumPopup("Type", bundle.type);
                    bundle.priority = EditorGUILayout.IntSlider("Priority", bundle.priority, 0, 10000);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _data.MarkAsDirty();
                    }
                });

                Block("Bundle Splits", () =>
                {
                    for (int splitIndex = 0, splitCount = bundle.splits.Count; splitIndex < splitCount; splitIndex++)
                    {
                        var split = bundle.splits[splitIndex];
                        var splitName = string.IsNullOrEmpty(split.name) ? "(default)" : split.name;
                        Foldout(splitName, () =>
                        {
                            var sliceCount = split.slices.Count;
                            EditorGUI.BeginChangeCheck();
                            var duplicated = IsDuplicated(bundle, split);
                            if (duplicated)
                            {
                                GUI.color = Color.yellow;
                                split.name = EditorGUILayout.TextField(
                                    Text("bundle.split.name", "Name", "warning: duplicated bundle split name"),
                                    split.name);
                                GUI.color = _GUIColor;
                            }
                            else
                            {
                                split.name = EditorGUILayout.TextField("Name", split.name);
                            }

                            split.encrypted = EditorGUILayout.Toggle("Encrypted?", split.encrypted);
                            split.sliceObjects = EditorGUILayout.IntField("Slice Objects", split.sliceObjects);
                            if (EditorGUI.EndChangeCheck())
                            {
                                _data.MarkAsDirty();
                            }

                            InspectRules(split.rules);
                            Block("Slices", () =>
                            {
                                for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
                                {
                                    var slice = split.slices[sliceIndex];
                                    if (sliceCount > 1)
                                    {
                                        var sliceName = string.Format("{0}/{1}: {2}", sliceIndex + 1, sliceCount,
                                            slice.name);
                                        EditorGUILayout.LabelField(sliceName);
                                    }
                                    else
                                    {
                                        EditorGUILayout.LabelField(slice.name);
                                    }

                                    for (var assetIndex = 0; assetIndex < slice.assetGuids.Count; assetIndex++)
                                    {
                                        var assetGuid = slice.assetGuids[assetIndex];
                                        var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                                        var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                                        EditorGUILayout.BeginHorizontal();
                                        GUILayout.Space(20f);
                                        EditorGUILayout.TextField(assetPath);
                                        EditorGUILayout.ObjectField(assetObject, typeof(Object), false);
                                        EditorGUILayout.EndHorizontal();
                                    }
                                }
                            });
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
                                    bundle.splits.Remove(split);
                                    bundle.splits.Insert(newSplitIndex, split);
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
                                    bundle.splits.Remove(split);
                                    bundle.splits.Insert(newSplitIndex, split);
                                    _data.MarkAsDirty();
                                });
                            }

                            EditorGUI.EndDisabledGroup();
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
                                        bundle.splits.Remove(split);
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
    }
}