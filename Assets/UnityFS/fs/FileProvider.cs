using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IFileProvider
    {
        bool Exists(string filename);
        byte[] ReadAllBytes(string filename);
    }
}
