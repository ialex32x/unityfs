using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    [Flags]
    [Serializable]
    public enum PackagePlatform
    {
        Any = 0, 
        Android = 1, 
        IOS = 2,
        Windows64 = 3, 
        MacOS = 4, 
    }
}