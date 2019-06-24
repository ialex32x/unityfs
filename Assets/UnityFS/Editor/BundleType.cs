using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;

    public enum BundleType
    {
        AssetBundle,
        SceneBundle,
        ZipArchive,
    }

    [Flags]
    public enum BundleAssetPlatforms
    {
        None = 0,
        Android = 1 << 0,
        iOS = 1 << 1,
        Any = Android | iOS,
    }

    [Flags]
    public enum BundleAssetTypes
    {
        None = 0,
        Any = 1 << 0,
        Prefab = 1 << 1,     // prefab object
        Animation = 1 << 2,  // animation object
        Material = 1 << 3,   // material object
        Texture = 1 << 4,    // any texture object files
        Javascript = 1 << 5, // *.js
        Sourcemap = 1 << 6,  // *.js.map
        Luascript = 1 << 7,  // *.lua
        Text = 1 << 8,       // *.txt
        Xml = 1 << 9,        // *.xml
        Json = 1 << 10,      // *.json
        Binary = 1 << 11,    // *.bytes (unity binary files)
    }
}
