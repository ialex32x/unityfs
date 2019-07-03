# unityfs

提供一个轻量实用的资源和文件访问层, 简化资源热更新项目的开发流程. 
开发过程中可以使用编辑器模式直接访问工程内的任意资源, 并在发布时无缝切换到通过 AssetBundle 访问资源.
配置文件与脚本代码独立压缩打包, 运行期直接读取压缩包访问文件, 且不受 Unity 文件命名限制.

# 目标特性
* 异步加载资源/场景
* 自动管理资源加载/卸载
* 自动管理资源更新
* 按优先级下载资源
* 支持断点续传
* 支持多下载源重试
* 支持边玩边下
* 可视化打包管理

# 进度
基本功能完成. <br/>
***打包过滤和自动分包功能尚未完成***. <br/>

# 实例

```csharp
using System;
using System.IO;
using System.Collections.Generic;

namespace Examples
{
    using UnityEngine;

    public class Sample : MonoBehaviour
    {
        public bool developMode;        // 编辑器模式 (直接从AssetDatabase加载, 无需打包)
        public bool downloadStartups;   // 是否进行启动包预下载

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
                var dataPath = string.IsNullOrEmpty(Application.temporaryCachePath) ? Application.persistentDataPath : Application.temporaryCachePath;
                var localPathRoot = Path.Combine(dataPath, "packages");
                Debug.Log($"open localPathRoot: {localPathRoot}");

                // 可用下载地址列表 (会依次重试, 次数超过地址数量时反复重试最后一个地址)
                // 适用于 CDN 部署还没有全部起作用时, 退化到直接文件服务器地址
                var urls = UnityFS.Utils.Helpers.URLs(
                    // "http://localhost:8081/",
                    "http://localhost:8080/"
                );
                UnityFS.Utils.Helpers.GetManifest(urls, localPathRoot, manifest =>
                {
                    // 可以进行预下载 (可选)
                    if (downloadStartups)
                    {
                        var startups = UnityFS.Utils.Helpers.CollectStartupBundles(manifest, localPathRoot);
                        UnityFS.Utils.Helpers.DownloadBundles(
                            localPathRoot, startups, urls, 
                            (i, all, task) =>
                            {
                                Debug.Log($"下载中 {startups[i].name}({task.url}) {(int)(task.progress * 100f)}% ({i}/{all})");
                            }, 
                            () =>
                            {
                                Debug.Log("全部下载完毕");
                                UnityFS.ResourceManager.Open(new UnityFS.BundleAssetProvider(manifest, localPathRoot, urls, 1));
                                OnUnityFSLoaded();
                            }
                        );
                    }
                    else
                    {
                        UnityFS.ResourceManager.Open(new UnityFS.BundleAssetProvider(manifest, localPathRoot, urls, 1));
                        OnUnityFSLoaded();
                    }
                });
            }
        }

        private void OnUnityFSLoaded()
        {
            // 获取核心脚本代码包
            var fs = UnityFS.ResourceManager.FindFileSystem("Assets/Examples/Config/test.txt");
            fs.completed += () =>
            {
                // 可以在这里由脚本接管后续启动流程
                // ScriptEngine.RunScript(fs.ReadAllBytes("Assets/Examples/Scripts/main.lua"));

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
                var scene = UnityFS.ResourceManager.LoadSceneAdditive("Assets/Examples/Scenes/test2.unity");
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

```

# Editor
![editorwindow](Assets/Examples/Textures/editorwindow.png)

# License
MIT
