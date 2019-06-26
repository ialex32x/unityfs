using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class Metadata
    {
        public const string Ext = ".meta"; // 文件名后缀

        public string checksum;
        public int size;
    }
}
