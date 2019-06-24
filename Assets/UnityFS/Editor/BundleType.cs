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
        Texture = 1 << 4,    // texture object
        Audio = 1 << 5,     // audio object
        Javascript = 1 << 6, // *.js
        Sourcemap = 1 << 7,  // *.js.map
        Luascript = 1 << 8,  // *.lua
        Xml = 1 << 9,        // *.xml
        Json = 1 << 10,      // *.json
        Text = 1 << 11,       // *.txt
        Binary = 1 << 12,    // *.bytes (unity binary files)
    }
}
