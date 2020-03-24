using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEditor.Callbacks;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(AssetListData))]
    public class AssetListDataInspector : Editor
    {
        private int _matchCount = 0;
        private string _text = string.Empty;

        public override void OnInspectorGUI()
        {
            var color = GUI.color;
            var data = target as AssetListData;
            data.timeSeconds = EditorGUILayout.FloatField("Time (Seconds)", data.timeSeconds);
            _text = EditorGUILayout.TextField("Find", _text);
            var count = data.timestamps.Count;
            if (!string.IsNullOrEmpty(_text))
            {
                EditorGUILayout.LabelField(string.Format("Assets: {0}/{1}", _matchCount, count));
            }
            else
            {
                EditorGUILayout.LabelField(string.Format("Assets: {0}", count));
            }

            _matchCount = 0;
            for (var i = 0; i < count; i++)
            {
                var ts = data.timestamps[i];
                var assetPath = AssetDatabase.GUIDToAssetPath(ts.guid);
                var assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                var match = string.IsNullOrEmpty(_text) || assetPath.Contains(_text);
                if (match)
                {
                    ++_matchCount;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.FloatField(ts.time, GUILayout.MaxWidth(100f));
                    EditorGUILayout.TextField(assetPath);
                    EditorGUILayout.ObjectField(assetObject, typeof(Object), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
}