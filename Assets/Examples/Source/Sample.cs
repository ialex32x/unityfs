using System;
using System.IO;
using System.Collections.Generic;

namespace Examples
{
    using UnityEngine;

    public class Sample : MonoBehaviour, UnityFS.IAssetProviderListener
    {
        public bool developMode;        // 编辑器模式 (直接从AssetDatabase加载, 无需打包)
        public bool downloadStartups;   // 是否进行启动包预下载

        public void OnStartupTask(UnityFS.Manifest.BundleInfo[] bundles)
        {
            var sum = 0;
            for (int i = 0, size = bundles.Length; i < size; i++)
            {
                sum += bundles[i].size;
            }
            var fsum = (float)sum / 1024f;
            string unit = null;
            if (fsum > 1024f)
            {
                fsum /= 1024f;
                unit = "MB";
            }
            else
            {
                unit = "KB";
            }
            Debug.Log($"需要下载文件数量: {bundles.Length} (共 {fsum.ToString("#.##")} {unit})");
        }

        public void OnComplete()
        {
        }

        public void OnTaskStart(UnityFS.ITask task)
        {
            Debug.Log($"task start: {task.name}");
        }

        public void OnTaskComplete(UnityFS.ITask task)
        {
            Debug.Log($"task complete: {task.name}");
        }

        void Awake()
        {
            Object.DontDestroyOnLoad(gameObject);

            // 可用下载地址列表 (会依次重试, 次数超过地址数量时反复重试最后一个地址)
            // 适用于 CDN 部署还没有全部起作用时, 退化到直接文件服务器地址
            var urls = UnityFS.Utils.Helpers.URLs(
                // "http://localhost:8081/",
                "http://localhost:8080/"
            );

            var dataPath = string.IsNullOrEmpty(Application.temporaryCachePath) ? Application.persistentDataPath : Application.temporaryCachePath;
            var localPathRoot = Path.Combine(dataPath, "bundles");
            Debug.Log($"open localPathRoot: {localPathRoot}");

            UnityFS.ResourceManager.Initialize(developMode, localPathRoot, urls, () =>
            {
                UnityFS.ResourceManager.SetListener(this); // [可选] 监听事件
            }, () =>
            {
                OnUnityFSLoaded();
            });
        }

        private void OnUnityFSLoaded()
        {
            // 获取核心脚本代码包
            UnityFS.ResourceManager.FindFileSystem("Assets/Examples/Config/test.txt").completed += fs =>
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
