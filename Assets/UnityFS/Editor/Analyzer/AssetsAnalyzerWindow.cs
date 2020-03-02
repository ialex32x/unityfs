using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    //TODO: analyzer sample
    public class AssetsAnalyzerWindow : EditorWindow, IAssetsAnalyzer
    {
        private AnalyzerTimeline _timeline;
        private string _csvPath = "assets_access_report.csv";
        private bool _pinned;
        private int _toFrameIndex;

        [MenuItem("UnityFS/Analyzer")]
        public static void OpenBuilderWindow()
        {
            GetWindow<AssetsAnalyzerWindow>().Show();
        }

        public void OnAssetAccess(string assetPath)
        {
            Debug.Log($"[analyzer] access {assetPath}");
            _timeline?.AccessAsset(assetPath);
        }

        public void OnAssetClose(string assetPath)
        {
            Debug.Log($"[analyzer] close {assetPath}");
            _timeline?.CloseAsset(assetPath);
        }

        public void OnAssetOpen(string assetPath)
        {
            Debug.Log($"[analyzer] open {assetPath}");
            _timeline?.OpenAsset(assetPath);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Assets Analyzer");
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
        }

        void OnUpdate()
        {
            if (!EditorApplication.isPaused)
            {
                _timeline.Update();
                Repaint();
                // if (Random.value > 0.59)
                // {
                //     var n = Random.Range(1, 10);
                //     for (var i = 0; i < n; i++)
                //     {
                //         _timeline.OpenAsset("test");
                //     }
                // }
            }
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    ResourceManager.SetAnalyzer(this);
                    _timeline = new AnalyzerTimeline();
                    _timeline.Start();
                    EditorApplication.update += OnUpdate;
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    _timeline.Stop();
                    EditorApplication.update -= OnUpdate;
                    ResourceManager.SetAnalyzer(null);
                    break;
            }
        }

        void WriteCSV(string path, AnalyzerAsset[] assets)
        {
            using (var fs = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write))
            {
                using (var ss = new StreamWriter(fs, Encoding.GetEncoding("gb2312")))
                {
                    ss.WriteLine("assetPath,time");
                    foreach (var asset in assets)
                    {
                        ss.WriteLine("{0},{1}", asset.assetPath, asset.firstOpenTime);
                    }
                }
            }
        }

        void OnGUI()
        {
            var csvPath = EditorGUILayout.TextField("CSV File Path", _csvPath);
            if (csvPath != _csvPath)
            {
                _csvPath = csvPath;
                EditorPrefs.SetString("unityfs.csv", _csvPath);
            }
            EditorGUI.BeginDisabledGroup(_timeline == null);
            if (GUILayout.Button("Save"))
            {
                var values = _timeline.assets.Values;
                var copy = new AnalyzerAsset[values.Count];
                values.CopyTo(copy, 0);
                WriteCSV(_csvPath, copy);
            }
            EditorGUI.EndDisabledGroup();
            if (_timeline == null)
            {
                EditorGUILayout.HelpBox("Idle", MessageType.Info);
                return;
            }
            EditorGUILayout.IntField("Frame", _timeline.frameIndex);
            EditorGUILayout.FloatField("Time", _timeline.frameTime);

            _toFrameIndex = _timeline.frameIndex;
            var graphX = 0f;
            var graphY = 60f;

            var margin = 10f;
            var frameCount = (int)(this.position.width - margin * 2f - graphX);
            var fromFrameIndex = _toFrameIndex - frameCount;
            var unitHeight = 10f;
            var graphHeight = 200f;
            var color = Handles.color;
            Handles.color = Color.green;
            for (var i = 0; i < frameCount; i++)
            {
                var frameIndex = _toFrameIndex - frameCount + i;
                var frame = _timeline.GetFrame(frameIndex);
                if (frame != null)
                {
                    var x = i + margin + graphX;
                    var y1 = graphY + (graphHeight - frame.assetCount * unitHeight);
                    var y2 = graphY + graphHeight;
                    Handles.DrawLine(new Vector3(x, y1), new Vector3(x, y2));
                }
            }
            Handles.color = color;
        }
    }
}
