using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;

    public enum BundleLoad
    {
        Startup,
        Normal,
    }

    [Flags]
    public enum BundleAssetPlatforms
    {
        Android = 1 << 0,
        iOS = 1 << 1,
        Windows = 1 << 2,
        OSX = 1 << 3,
        Linux = 1 << 4,
    }

    [Flags]
    public enum BundleAssetTypes
    {
        Prefab = 1 << 0,     // prefab object
        TextAsset = 1 << 1,
        Animation = 1 << 2,  // animation object
        Material = 1 << 3,   // material object
        Texture = 1 << 4,    // texture object
        Audio = 1 << 5,     // audio object

        // Javascript = 1 << 20, // *.js
        // Sourcemap = 1 << 21,  // *.js.map
        // Luascript = 1 << 22,  // *.lua
        // Xml = 1 << 23,        // *.xml
        // Json = 1 << 24,       // *.json
        // Text = 1 << 25,       // *.txt
        // Binary = 1 << 26,     // *.bytes (unity binary files)
    }
}
