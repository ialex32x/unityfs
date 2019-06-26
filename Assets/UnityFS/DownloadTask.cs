using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.Networking;

    public class DownloadTask
    {
        public int retry;       // 重试次数 (<0 时无限重试)
        public string tempPath; // 临时路径
        public string filePath; // 最终路径

        private UBundle _bundle;
        private bool _running;
        private int _urlIndex;
        private IList<string> _urls;

        private bool _isDone;
        private string _error;

        public Manifest.BundleInfo bundleInfo
        {
            get { return _bundle.bundleInfo; }
        }

        // 运行中
        public bool isRunning
        {
            get { return _running; }
        }

        // 是否已完成 
        public bool isDone
        {
            get { return _isDone; }
        }

        // 错误信息 (null 表示没有错误)
        public string error
        {
            get { return _error; }
        }

        // 当前请求的url
        public string url
        {
            get
            {
                if (_urls[_urlIndex].EndsWith("/"))
                {
                    return _urls[_urlIndex] + _bundle.name;
                }
                return _urls[_urlIndex] + "/" + _bundle.name;
            }
        }

        // invoke in main thread
        private Action<DownloadTask> _callback;

        public DownloadTask(UBundle bundle, IList<string> urls, int retry, Action<DownloadTask> callback)
        {
            this._urls = urls;
            this._bundle = bundle;
            this._bundle.AddRef();
            this._callback = callback;

            this.retry = retry;
            this.tempPath = "TODO"; // TODO: generate temp file path
            this.filePath = "TODO"; // 
        }

        public void Run()
        {
            _running = true;
            JobScheduler.DispatchCoroutine(DownloadExec());
        }

        private bool Retry()
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

        private IEnumerator DownloadExec()
        {
            while (true)
            {
                var url = this.url;
                var req = UnityWebRequest.Get(url);
                var partialSize = 0;
                var totalSize = this._bundle.size;
                req.SetRequestHeader("Range", $"bytes={partialSize}-{totalSize}");
                yield return req.SendWebRequest();
                long contentLength;
                if (!long.TryParse(req.GetResponseHeader("Content-Length"), out contentLength))
                {
                    if (!this.Retry())
                    {
                        this.Complete("too many retry");
                        yield break;
                    }
                    continue;
                }
                //TODO: download content ...
            }
        }

        private void Complete(string error)
        {
            if (!_isDone)
            {
                _error = error;
                _isDone = true;
                _running = false;
                this._bundle.RemoveRef();
            }
        }

        // return stream of downloaded file
        public Stream OpenFile()
        {
            throw new NotImplementedException();
        }
    }
}
