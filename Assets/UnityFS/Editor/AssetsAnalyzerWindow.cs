using System;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    //TODO: analyzer sample
    public class AssetsAnalyzerWindow : EditorWindow, IAssetsAnalyzer
    {
        [MenuItem("UnityFS/Analyzer")]
        public static void OpenBuilderWindow()
        {
            GetWindow<AssetsAnalyzerWindow>().Show();
        }

        public void OnAssetAccess(string assetPath)
        {
            Debug.Log($"[analyzer] access {assetPath}");
        }

        public void OnAssetClose(string assetPath)
        {
            Debug.Log($"[analyzer] close {assetPath}");
        }

        public void OnAssetOpen(string assetPath)
        {
            Debug.Log($"[analyzer] open {assetPath}");
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Assets Analyzer");
            ResourceManager.SetAnalyzer(this);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ResourceManager.SetAnalyzer(null);
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    break;
            }
        }

        void OnGUI()
        {
            var analyzer = ResourceManager.GetAnalyzer() as AssetsAnalyzerWindow;
            if (analyzer == this)
            {
                EditorGUILayout.HelpBox("Running", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Idle", MessageType.Info);
            }
        }
    }
}
