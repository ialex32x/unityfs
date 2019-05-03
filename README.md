# unityfs

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
