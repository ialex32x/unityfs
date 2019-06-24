using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    public class BundleBuilderWindow : EditorWindow
    {
        private bool dirty;
        private BundleBuilderData data;

        [MenuItem("UnityFS/Builder")]
        public static void OpenBuilderWindow()
        {
            GetWindow<BundleBuilderWindow>().Show();
        }

        void OnEnable()
        {
            data = BundleBuilder.GetData();
            titleContent = new GUIContent("Bundle Builder");
        }

        void OnGUI()
        {
            if (GUILayout.Button("Add Bundle"))
            {
                data.bundles.Add(new BundleBuilderData.BundleInfo());
            }

            for (var bundleIndex = 0; bundleIndex < data.bundles.Count; bundleIndex++)
            {
                var bundle = data.bundles[bundleIndex];
                EditorGUI.BeginChangeCheck();
                bundle.name = EditorGUILayout.TextField("Name", bundle.name);
                if (GUILayout.Button("Scan"))
                {
                    BundleBuilder.Scan(bundle);
                    dirty = true;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }

            if (GUILayout.Button("Build"))
            {
                BundleBuilder.Build(data, "out/AssetBundles", EditorUserBuildSettings.activeBuildTarget);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(data);
                dirty = false;
            }
        }
    }
}
