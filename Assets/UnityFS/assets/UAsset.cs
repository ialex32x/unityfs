using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class UAsset
    {
        private Object _object;

        public Action Loaded;

        // 同步加载资源, 如果加载成功则返回 Object, 否则返回 null
        public Object LoadSync()
        {
            throw new NotImplementedException();
        }

        // 异步加载
        public bool Load()
        {
            throw new NotImplementedException();
        }
    }
}
