using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class DownloadTask
    {
        public int size;        // 期望大小 (=0 时忽略)
        public int retry;       // 重试次数 (<0 时无限重试)
        public int priority;    // 优先级 
        public string checksum; // 校验值 (=null 时忽略)
        public string tempPath; // 临时路径
        public string filePath; // 最终路径

        // invoke in main thread
        private Action<DownloadTask> _callback;

        public DownloadTask(Manifest.BundleInfo bundleInfo, int retry, Action<DownloadTask> callback)
        {
            this.retry = retry;
            this.size = bundleInfo.size;
            this.priority = bundleInfo.priority;
            this.checksum = bundleInfo.checksum;
            this.tempPath = "TODO"; // generate temp file path
            this.filePath = "TODO"; // 
            _callback = callback;
        }

        // return stream of downloaded file
        public Stream OpenFile()
        {
            throw new NotImplementedException();
        }
    }
}
