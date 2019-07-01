using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public interface IRefCounted
    {
        void AddRef();
        void RemoveRef();
    }
}
