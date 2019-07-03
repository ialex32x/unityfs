using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.Networking;

    public interface ITask
    {
        float progress { get; }
        int size { get; }
        string name { get; }
        string path { get; }
        string url { get; }
    }
}
