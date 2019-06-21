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
            UnityFS.JobScheduler.Initialize();
            if (developMode)
            {
                UnityFS.ResourceManager.SetAssetProvider(new UnityFS.AssetDatabaseAssetProvider());
                UnityFS.ResourceManager.SetFileProvider(new UnityFS.OrdinaryFileProvider());
            }
            else
            {
                // 获取远程校验值
                // 原始访问本地 manifest, 对比校验值
                // 确定最新 manifest
                // 创建 ManifestFileProvider/BundleAssetProvider
                // 加载代码包, 产生一个新的 ZipFileProvider 传递给 ScriptEngine (Exists/ReadAllBytes)
                // 后续加载流程由脚本接管
            }

            var fs = new FakeFileSystem();
            var data = fs.ReadAllBytes("Assets/test.txt");
            Debug.Log(System.Text.Encoding.UTF8.GetString(data));
        }
    }
}
