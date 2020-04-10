using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    public class BaseEditorWindow : EditorWindow
    {
        protected static GUIStyle _foldoutArea = new GUIStyle();
        protected static GUIStyle _blockStyle = new GUIStyle();
        protected static GUIStyle _foldoutStyle = new GUIStyle();
        protected Dictionary<string, GUIContent> _titles = new Dictionary<string, GUIContent>();
        protected List<Action> _defers = new List<Action>();
        protected Color _GUIColor;

        private BuildTarget _targetPlatform;

        protected virtual void OnDisable()
        {
        }

        protected virtual void OnEnable()
        {
            _blockStyle.normal.background = MakeTex(100, 100, new Color32(56, 56, 56, 0));
            _foldoutStyle.alignment = TextAnchor.MiddleLeft;
        }

        protected virtual void OnGUIDraw()
        {
            
        }

        private void OnGUI()
        {
            _foldoutStyle.normal.textColor = GUI.skin.button.normal.textColor;
            _GUIColor = GUI.color;
            OnGUIDraw();
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
            Handles.DrawLine(new Vector3(rect.xMin, rect.yMin + rect.height * 0.5f),
                new Vector3(rect.xMax, rect.yMin + rect.height * 0.5f));
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector3(rect.xMin + 1f, rect.yMin + rect.height * 0.5f + 1f),
                new Vector3(rect.xMax, rect.yMin + rect.height * 0.5f + 1f));
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
    }
}