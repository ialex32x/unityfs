using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BundleAssetsWindow : EditorWindow
    {
        protected static GUIStyle _foldoutArea = new GUIStyle();
        protected static GUIStyle _blockStyle = new GUIStyle();
        protected static GUIStyle _foldoutStyle = new GUIStyle();
        protected Dictionary<string, GUIContent> _titles = new Dictionary<string, GUIContent>();
        protected List<Action> _defers = new List<Action>();
        private BundleBuilderData _data;
        private IList<BundleBuilderData.BundleInfo> _bundles;
        private Vector2 _sv;
        private Color _GUIColor;

        private BuildTarget _targetPlatform;

        void OnEnable()
        {
            titleContent = new GUIContent("Bundle Editor");
            _blockStyle.normal.background = MakeTex(100, 100, new Color32(56, 56, 56, 0));
            _foldoutStyle.alignment = TextAnchor.MiddleLeft;
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
            _targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            BundleBuilder.Scan(_data, _targetPlatform);
        }

        void OnGUI()
        {
            _foldoutStyle.normal.textColor = GUI.skin.button.normal.textColor;
            _GUIColor = GUI.color;
            if (_bundles == null || _bundles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }
            _sv = GUILayout.BeginScrollView(_sv);
            var rescan = false;
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            var targetPlatform = (BuildTarget)EditorGUILayout.EnumPopup("Preview Platform", _targetPlatform);
            if (GUILayout.Button("Reset", GUILayout.Width(120f)))
            {
                _targetPlatform = targetPlatform = EditorUserBuildSettings.activeBuildTarget;
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
            {
                rescan = true;
            }
            GUILayout.EndHorizontal();
            if (_targetPlatform != targetPlatform)
            {
                _targetPlatform = targetPlatform;
                rescan = true;
            }
            if (rescan)
            {
                BundleBuilder.Scan(_data, _targetPlatform);
            }
            foreach (var bundle in _bundles)
            {
                InspectBundle(bundle);
            }
            GUILayout.EndScrollView();
            ExecuteDefers();
        }

        protected Texture2D MakeTex(int width, int height, Color fillColor)
        {
            var pixels = new Color[width * height];
            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var point = x + y * width;
                    pixels[point] = fillColor;
                }
            }
            var result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        protected void BorderLine(Rect rect)
        {
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(rect.xMin, rect.yMin + rect.height * 0.5f), new Vector3(rect.xMax, rect.yMin + rect.height * 0.5f));
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector3(rect.xMin + 1f, rect.yMin + rect.height * 0.5f + 1f), new Vector3(rect.xMax, rect.yMin + rect.height * 0.5f + 1f));
        }

        protected void BorderLine(float x1, float y1, float x2, float y2)
        {
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector3(x1 + 1f, y1 + 1f), new Vector3(x2, y2));
        }

        protected bool Foldout(string text, Action content, params Action[] items)
        {
            return Foldout(text, text, content, true, items);
        }

        protected bool Foldout(string text, Action content, bool defaultValue, params Action[] items)
        {
            return Foldout(text, text, content, defaultValue, items);
        }

        protected bool Foldout(string key, string text, Action content, bool defaultValue, params Action[] items)
        {
            var prefid = $"{GetType().FullName}:Foldouts:{key}";
            var foldout = EditorPrefs.GetBool(prefid, defaultValue);
            EditorGUILayout.BeginHorizontal();
            var headerRect = EditorGUILayout.GetControlRect();
            // var newfoldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, text);
            GUI.Box(headerRect, "");
            if (GUI.Button(headerRect, (foldout ? "▼ " : "▶ ") + text, _foldoutStyle))
            {
                foldout = !foldout;
                EditorPrefs.SetBool(prefid, foldout);
            }
            for (var i = 0; i < items.Length; i++)
            {
                items[i]();
            }
            EditorGUILayout.EndHorizontal();
            if (foldout)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(6f);
                EditorGUILayout.BeginVertical(_foldoutArea);
                content();
                GUILayout.Space(4f);
                // GUILayout.Box("", GUILayout.Height(2f), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            // EditorGUILayout.EndFoldoutHeaderGroup();
            return foldout;
        }

        protected void Block(string title, Action contentDrawer, params Action[] utilities)
        {
            Block(() => GUILayout.Label(title, GUILayout.ExpandWidth(false)), contentDrawer, utilities);
        }

        protected void Block(Action titleDrawer, Action contentDrawer, params Action[] utilities)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical(_blockStyle);
            EditorGUILayout.BeginHorizontal();
            titleDrawer(); // GUILayout.Label(title, GUILayout.ExpandWidth(false));
            var rectBegin = EditorGUILayout.GetControlRect(true, GUILayout.ExpandWidth(true));
            var color = Handles.color;
            BorderLine(rectBegin);
            Handles.color = color;
            for (var i = 0; i < utilities.Length; i++)
            {
                utilities[i]();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical();
            contentDrawer();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10f);
            var rectEnd = EditorGUILayout.GetControlRect(true, GUILayout.Height(1f));
            BorderLine(rectEnd);
            BorderLine(rectEnd.xMin, rectBegin.yMax, rectEnd.xMin, rectEnd.yMax);
            BorderLine(rectEnd.xMax, (rectBegin.yMin + rectBegin.yMax) * 0.5f, rectEnd.xMax, rectEnd.yMax);
            Handles.color = color;
            GUILayout.Space(2f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public GUIContent Text(string key, string name, string tooltip = "")
        {
            GUIContent content;
            if (!_titles.TryGetValue(key, out content))
            {
                _titles[key] = content = new GUIContent(name, tooltip);
            }
            return content;
        }

        protected void Defer(Action action)
        {
            _defers.Add(action);
        }

        protected void ExecuteDefers()
        {
            var size = _defers.Count;
            if (size > 0)
            {
                var list = new Action[size];
                _defers.CopyTo(list, 0);
                _defers.Clear();
                for (var i = 0; i < size; i++)
                {
                    list[i]();
                }
            }
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
                    rule.assetTypes = (BundleAssetTypes)EditorGUILayout.EnumFlagsField(rule.assetTypes, GUILayout.Width(80f));
                    rule.type = (BundleBuilderData.BundleSplitType)EditorGUILayout.EnumPopup(rule.type, GUILayout.Width(80f));
                    rule.keyword = EditorGUILayout.TextField(rule.keyword, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
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
                    var note = EditorGUILayout.TextField("Info", bundle.note);
                    if (note != bundle.note)
                    {
                        bundle.note = note;
                        _data.MarkAsDirty();
                    }
                });

                Block("Bundle Splits", () =>
                {
                    for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
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
                                split.name = EditorGUILayout.TextField(Text("bundle.split.name", "Name", "warning: duplicated bundle split name"), split.name);
                                GUI.color = _GUIColor;
                            }
                            else
                            {
                                split.name = EditorGUILayout.TextField("Name", split.name);
                            }
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
                                        var sliceName = string.Format("{0}/{1}: {2}", sliceIndex + 1, sliceCount, slice.name);
                                        EditorGUILayout.LabelField(sliceName);
                                    }
                                    else
                                    {
                                        EditorGUILayout.LabelField(slice.name);
                                    }
                                    for (var assetIndex = 0; assetIndex < slice.assets.Count; assetIndex++)
                                    {
                                        var asset = slice.assets[assetIndex];
                                        var assetPath = string.Empty;
                                        if (asset != null)
                                        {
                                            assetPath = AssetDatabase.GetAssetPath(asset);
                                        }
                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.TextField(assetPath);
                                        EditorGUILayout.ObjectField(asset, typeof(Object), false);
                                        EditorGUILayout.EndHorizontal();
                                    }
                                }
                            });
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
