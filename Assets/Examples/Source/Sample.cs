using System;
using System.IO;
using System.Collections.Generic;

namespace Examples
{
    using UnityEngine;

    public class Sample : MonoBehaviour
    {
        public bool developMode;

        void Awake()
        {
            if (developMode)
            {
                UnityFS.ResourceManager.Initialize(new UnityFS.AssetDatabaseAssetProvider());
            }
            else
            {
                var manifest = new UnityFS.Manifest();  // TODO: STUB CODE
                var localPathRoot = string.Empty;       // TODO: STUB CODE
                var urls = UnityFS.ResourceManager.URLs(
                    "http://localhost:8080/"
                );
                UnityFS.ResourceManager.Initialize(new UnityFS.BundleAssetProvider(manifest, localPathRoot, urls));
                // 获取远程校验值
                // 原始访问本地 manifest, 对比校验值
                // 确定最新 manifest
                // 创建 ManifestFileProvider/BundleAssetProvider
                // 加载代码包, 产生一个新的 ZipFileProvider 传递给 ScriptEngine (Exists/ReadAllBytes)
                // 后续加载流程由脚本接管
            }
            var fs = new FakeFileSystem();
            var data = fs.ReadAllBytes("Assets/Examples/Config/test.txt");
            Debug.Log(System.Text.Encoding.UTF8.GetString(data));
            {
                var asset = UnityFS.ResourceManager.LoadAsset("Assets/Examples/Prefabs/Cube 1.prefab");
                asset.completed += self =>
                {
                    var gameObject = Object.Instantiate(self.GetObject()) as GameObject;
                    UnityFS.Utils.AssetHandle.Attach(gameObject, asset, 5.0f);
                };
                var loader = UnityFS.Utils.PrefabLoader.Instantiate("Assets/Examples/Prefabs/Cube 1.prefab");
                loader.StartCoroutine(UnityFS.Utils.Helpers.DestroyAfter(loader.gameObject, 10.0f));
            }
        }
    }
}
