using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IFileSystem
    {
        event Action<IFileSystem> completed; // 加载完成

        bool Exists(string filename);
        Stream OpenRead(string filename);
        byte[] ReadAllBytes(string filename);
    }
}
