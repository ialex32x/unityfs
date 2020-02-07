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
        protected static GUIStyle _blockStyle = new GUIStyle();
        protected Dictionary<string, GUIContent> _titles = new Dictionary<string, GUIContent>();
        protected List<Action> _defers = new List<Action>();
        private BundleBuilderData _data;
        private IList<BundleBuilderData.BundleInfo> _bundles;
        private Vector2 _sv;
        private Color _GUIColor;

        private BuildTarget _targetPlatform;

        void OnEnable()
        {
            titleContent = new GUIContent("Bundle Report");
            _blockStyle.normal.background = MakeTex(100, 100, new Color32(56, 56, 56, 0));
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
            _GUIColor = GUI.color;
            if (_bundles == null || _bundles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing", MessageType.Warning);
                return;
            }
            _sv = GUILayout.BeginScrollView(_sv);
            var rescan = false;
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
            GUILayout.Space(20f);
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
                GUILayout.Space(50f);
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

        private void InspectBundle(BundleBuilderData.BundleInfo bundle)
        {
            var bundleName = string.IsNullOrEmpty(bundle.name) ? "(null)" : bundle.name;
            EditorGUILayout.HelpBox($"{bundleName}", MessageType.Info);
            var note = EditorGUILayout.TextField("Info", bundle.note);
            if (note != bundle.note)
            {
                bundle.note = note;
                _data.MarkAsDirty();
            }
            Block("Rules", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Type", GUILayout.Width(80f));
                EditorGUILayout.LabelField("Keyword", GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField("Name", GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField("Slice", GUILayout.Width(40f));
                GUILayout.Space(20f);
                EditorGUILayout.EndHorizontal();

                for (int i = 0, size = bundle.rules.Count; i < size; i++)
                {
                    var rule = bundle.rules[i];
                    EditorGUILayout.BeginHorizontal();
                    rule.type = (BundleBuilderData.BundleSplitType)EditorGUILayout.EnumPopup(rule.type, GUILayout.Width(80f));
                    rule.keyword = EditorGUILayout.TextField(rule.keyword, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                    rule.name = EditorGUILayout.TextField(rule.name, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                    rule.capacity = EditorGUILayout.IntField(rule.capacity, GUILayout.Width(40f));
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
                                bundle.rules.Remove(rule);
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
                if (GUI.Button(rect, Text("add.rule", "+", "添加分包规则")))
                {
                    Defer(() =>
                    {
                        bundle.rules.Add(new BundleBuilderData.BundleSplitRule());
                        _data.MarkAsDirty();
                    });
                }
                GUI.color = _GUIColor;
            });

            GUILayout.Space(4f);
            for (var splitIndex = 0; splitIndex < bundle.splits.Count; splitIndex++)
            {
                var split = bundle.splits[splitIndex];
                if (EditorGUILayout.Foldout(true, "Split: " + (split.name ?? "(default)")))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20f);
                    EditorGUILayout.BeginVertical();
                    for (var sliceIndex = 0; sliceIndex < split.slices.Count; sliceIndex++)
                    {
                        var slice = split.slices[sliceIndex];
                        EditorGUILayout.LabelField("Slice: " + sliceIndex);

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(20f);
                        EditorGUILayout.BeginVertical();
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
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}
