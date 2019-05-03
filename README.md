# unityfs

提供一个轻量可用的资源和文件访问层

目标特性
* 支持异步/同步加载方式
* 自动管理资源加载/卸载
* 按优先级下载assetbundle
* 文件下载支持断点续传
* 压缩包内文件加载

```c
Layer0
    AssetProvider
        AssetbundleAssetProvider
        AssetdatabaseAssetProvider
    SceneProvider
Layer1
    FileProvider (supports file checksum/size query)
        StreamingAssetFileProvider
        OrdinaryFIleProvider
    ArchiveFileProvider
    RemoteFileProvider (write storage)
```