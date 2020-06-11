using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;
using UnityFS;
using UnityFS.Utils;

namespace Examples
{
    using UnityEngine;

    public class Sample : MonoBehaviour, UnityFS.IAssetProviderListener
    {
        public bool developMode;        // 编辑器模式 (直接从AssetDatabase加载, 无需打包)
        public int bufferSize = 0;
        public string password = "test"; // 危险, 真实环境中从远端获取密码
        public string listDataPath;
        public Image image;

        private UnityFS.Utils.PrefabPools _pools;

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

        public void OnSetManifest()
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

        void Start()
        {
            Object.DontDestroyOnLoad(gameObject);
            _pools = new UnityFS.Utils.PrefabPools(new GameObject("_GameObjectPool"));
            gameObject.AddComponent<UnityFS.Utils.TaskInspector>();

            // 可用下载地址列表 (会依次重试, 次数超过地址数量时反复重试最后一个地址)
            // 适用于 CDN 部署还没有全部起作用时, 退化到直接文件服务器地址
            var urls = UnityFS.Utils.Helpers.URLs(
                // "http://localhost:8081/",
                "http://localhost:8080/"
            );

            var dataPath = string.IsNullOrEmpty(Application.temporaryCachePath) ? Application.persistentDataPath : Application.temporaryCachePath;
            var localPathRoot = Path.Combine(dataPath, "packages");
            Debug.Log($"open localPathRoot: {localPathRoot}");

            UnityFS.ResourceManager.Initialize(new UnityFS.ResourceManagerArgs()
            {
                listDataPath = listDataPath, 
                devMode = developMode,
                bufferSize = bufferSize,
                localPathRoot = localPathRoot,
                urls = urls,
                asyncSimMin = 0.5f,
                asyncSimMax = 1f,
                password = password,
                oninitialize = () =>
                {
                    UnityFS.ResourceManager.SetListener(this); // [可选] 监听事件
                },
                oncomplete = () =>
                {
                    OnUnityFSLoaded();
                }
            });
        }

        private void OnUnityFSLoaded()
        {
            var bundles = ResourceManager.GetInvalidatedBundles(Manifest.BundleLoad.Startup);
            UnityFS.ResourceManager.EnsureBundles(bundles, OnAllStartupsLoaded);
        }
    
        private void OnAllStartupsLoaded()
        {
            // 在 zip 包中的文件可以异步加载
            var testFile = UnityFS.ResourceManager.LoadAsset("Assets/Examples/Config/test.txt");
            testFile.completed += self =>
            {
                var text = System.Text.Encoding.UTF8.GetString(testFile.ReadAllBytes());
                Debug.Log($"用 LoadAsset 形式加载一个文件: {text}");
            };

            // 在 zip 包中的文件也可以异步加载
            // 首先获取对应的 FileSystem 对象， 并等待加载完成，完成后可同步加载其中的所有内容
            UnityFS.ResourceManager.FindFileSystem("Assets/Examples/Config/test.txt").completed += fs =>
            {
                // 可以在这里由脚本接管后续启动流程
                // ScriptEngine.RunScript(fs.ReadAllBytes("Assets/Examples/Scripts/main.lua"));
                
                // 具体游戏实现中可以自己实现一个多层文件加载, 提供给脚本系统或者配置读取模块
                // 这样如果前层 FileSystem 中存在文件, 则优先加载
                // 例如对于已经打包后的配置, 在不重新打包的情况下优先读取目录中直接存在的文件, 可以用于非编辑器环境下临时调试修改等
                var cfs = new CompositeFileSystem();
                cfs.AddFileSystems(new OrdinaryFileSystem(), fs);
                var readmeBytes = cfs.ReadAllBytes("README.md");
                Debug.Log(readmeBytes != null ? Encoding.UTF8.GetString(readmeBytes) : "readme.md not exists");

                // 其他接口示意:

                // 读取文件内容 (zip包中的文件可以同步读取)
                // NOTE: ** FileSystem 加载完成后， 只要持有其引用， 其中的文件均可同步加载
                var data = fs.ReadAllBytes("Assets/Examples/Config/test.txt");
                Debug.Log(System.Text.Encoding.UTF8.GetString(data));

                // 加载资源, 得到原始 Asset 对象
                UnityFS.ResourceManager.LoadAsset("Assets/Examples/Prefabs/Cube 1.prefab", self => UnityFS.Utils.AssetHandle.CreateInstance(self, 5.0f));
                UnityFS.ResourceManager.LoadAsset("Assets/Examples/Prefabs/Cube 2.prefab", self => UnityFS.Utils.AssetHandle.CreateInstance(self, 5.0f));

                UnityFS.Utils.PrefabLoader.Load("Assets/Examples/Prefabs/Cube 1.prefab");

                if (image != null)
                {
                    ResourceManager.LoadAsset<Sprite>("Assets/Examples/Textures/Atlas/9.png", asset =>
                    {
                        AssetHandle.Attach(image.gameObject, asset);
                        image.sprite = asset.GetObject<Sprite>("9");
                    });
                }

                var handle = _pools.Alloc("Assets/Examples/Prefabs/Cube 9.prefab");
                StartCoroutine(UnityFS.Utils.Helpers.InvokeAfter(() =>
                {
                    handle.Release();
                }, 10f));

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

                var fileList = UnityFS.ResourceManager.LoadAsset("Assets/Examples/Files");
                fileList.completed += self =>
                {
                    var fileListManifest = self.GetValue() as UnityFS.FileListManifest;
                    if (fileListManifest != null)
                    {
                        for (int i = 0, size = fileListManifest.files.Count; i < size; i++)
                        {
                            var item = fileListManifest.files[i];
                            Debug.LogFormat("file list entry:{0} size:{1} check:{2}", item.name, item.size, item.checksum);
                        }
                    }
                };
            };
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Test ReadSA"))
            {
                Helpers.ReadSAManifest("test", (manifest, fileEntry) =>
                {
                    Debug.LogFormat("Read SA Manifest: {0}", manifest.build);
                });
            }
            if (GUILayout.Button("Validate"))
            {
                ResourceManager.ValidateManifest(ResourceManager.urls, result =>
                {
                    Debug.LogFormat("当前清单状态: {0}", result);
                });
            }
        }
    }
}
