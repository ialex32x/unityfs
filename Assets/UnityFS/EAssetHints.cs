using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    [Flags]
    public enum EAssetHints
    {
        None = 0, 
        Synchronized = 1, // 尝试同步载入
    }
}
