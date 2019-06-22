# unityfs

提供一个轻量实用的资源和文件访问层, 简化资源热更新项目的开发流程. 
开发过程中可以使用编辑器模式直接访问工程内的任意资源, 并在发布时无缝切换到通过 AssetBundle 访问资源.
配置文件与脚本代码独立压缩打包, 运行期直接读取压缩包访问文件, 且不受 Unity 文件命名限制.

# 目标特性
* 异步加载资源
* 自动管理资源加载/卸载
* 自动管理资源更新
* 按优先级下载资源
* 支持断点续传
* 支持多下载源重试
* 支持边玩边下
* 可视化打包管理

# 进度
未完成

# 实例

```csharp

// 直接加载并实例化 prefab 资源 (UAsset 的强引用由此 GameObject 保持)
UnityFS.Utils.PrefabLoader.Instantiate("Assets/Examples/Prefabs/Cube.prefab");

// 通过 LoadAsset 获取 UAsset 资源对象
var asset = UnityFS.ResourceManager.LoadAsset("Assets/Examples/Prefabs/TestMaterial.material");
asset.completed += self =>
{
    // self.GetObject();
};
// 需要保持对 asset 的强引用, 否则 asset 将自动被 gc

```

# License
MIT
