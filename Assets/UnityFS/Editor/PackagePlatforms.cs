using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS.Editor
{
    using UnityEngine;
    using UnityEditor;

    [Flags]
    public enum PackagePlatforms
    {
        Active = 1, 
        Android = 2, 
        IOS = 4,
        Windows64 = 8, 
        MacOS = 16, 
    }
}