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
            Object.DontDestroyOnLoad(gameObject);

            UnityFS.ResourceManager.Initialize();
#if UNITY_EDITOR
            if (developMode)
            {
                UnityFS.ResourceManager.Open(new UnityFS.AssetDatabaseAssetProvider());
                OnUnityFSLoaded();
            }
            else
#endif
            {
                // 获取远程校验值 checksum.txt
                // 访问本地 manifest, 对比校验值 checksum
                // 确定最新 manifest
                // 创建 BundleAssetProvider
                // 加载代码包, 产生一个新的 (Zip)FileSystem 传递给脚本引擎 (Exists/ReadAllBytes)
                // 后续启动流程可由脚本接管
                var dataPath = string.IsNullOrEmpty(Application.temporaryCachePath) ? Application.persistentDataPath : Application.temporaryCachePath;
                var localPathRoot = Path.Combine(dataPath, "packages");
                Debug.Log($"open localPathRoot: {localPathRoot}");
                var urls = UnityFS.Utils.Helpers.URLs(
                    "http://localhost:8080/"
                );
                UnityFS.Utils.Helpers.GetManifest(urls, localPathRoot, manifest =>
                {
                    // 可用下载地址列表 (会依次重试, 次数超过地址数量时反复重试最后一个地址)
                    // 适用于 CDN 部署还没有全部起作用时, 退化到直接文件服务器地址
                    UnityFS.ResourceManager.Open(new UnityFS.BundleAssetProvider(manifest, localPathRoot, urls));
                    OnUnityFSLoaded();
                });
            }
        }

        private void OnUnityFSLoaded()
        {
            // 获取核心脚本代码包
            var fs = UnityFS.ResourceManager.GetFileSystem("test2.pkg");
            fs.completed += () =>
            {
                // 可以在这里由脚本接管后续启动流程
                // ScriptEngine.RunScript(fs.ReadAllBytes("Assets/Examples/Scripts/main.lua));

                // 其他接口示意:

                // 读取文件内容 (zip包中的文件可以同步读取)
                var data = fs.ReadAllBytes("Assets/Examples/Config/test.txt");
                Debug.Log(System.Text.Encoding.UTF8.GetString(data));

                // 加载资源, 得到原始 Asset 对象
                UnityFS.ResourceManager.LoadAsset("Assets/Examples/Prefabs/Cube 1.prefab", self =>
                {
                    UnityFS.Utils.AssetHandle.CreateInstance(self, 5.0f);
                });
                // 加载资源, 通过辅助方法直接创建 GameObject
                UnityFS.ResourceManager.Instantiate("Assets/Examples/Prefabs/Cube 1.prefab")
                    .DestroyAfter(10.0f);
                // 当所有对象均不引用一个 AssetBundle 时, 将自动卸载对应 AssetBundle

                // 加载场景
                var scene = UnityFS.ResourceManager.LoadSceneAdditive("Assets/Examples/Scenes/test2.unity3d");
                scene.completed += self =>
                {
                    Debug.Log("scene loaded");
                };

                StartCoroutine(UnityFS.Utils.Helpers.InvokeAfter(() =>
                {
                    scene.UnloadScene();
                    scene = null;
                }, 20f));
            };
        }
    }
}
