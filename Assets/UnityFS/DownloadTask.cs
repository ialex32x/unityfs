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
        public string name;

        private int _urlIndex;
        private IList<string> _urls;

        private bool _isDone;
        private string _error;

        public bool isDone
        {
            get { return _isDone; }
        }

        public string error
        {
            get { return _error; }
        }

        public string url
        {
            get
            {
                if (_urls[_urlIndex].EndsWith("/"))
                {
                    return _urls[_urlIndex] + name;
                }
                return _urls[_urlIndex] + "/" + name;
            }
        }

        // invoke in main thread
        private Action<DownloadTask> _callback;

        public DownloadTask(Manifest.BundleInfo bundleInfo, IList<string> urls, int retry, Action<DownloadTask> callback)
        {
            this._urls = urls;
            this.retry = retry;
            this.name = bundleInfo.name;
            this.size = bundleInfo.size;
            this.priority = bundleInfo.priority;
            this.checksum = bundleInfo.checksum;
            this.tempPath = "TODO"; // generate temp file path
            this.filePath = "TODO"; // 
            _callback = callback;
        }

        public bool Retry()
        {
            if (retry > 0 && (_urlIndex + 1) >= retry)
            {
                return false;
            }
            if (_urlIndex < _urls.Count - 1)
            {
                ++_urlIndex;
            }
            return true;
        }

        public void Complete(string error)
        {
            _error = error;
            _isDone = true;
        }

        // return stream of downloaded file
        public Stream OpenFile()
        {
            throw new NotImplementedException();
        }
    }
}
