using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IFileSystem
    {
        event Action completed; // 加载完成

        bool Exists(string filename);
        byte[] ReadAllBytes(string filename);
    }
}
