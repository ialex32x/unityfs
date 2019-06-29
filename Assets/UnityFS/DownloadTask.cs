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

        private DownloadTask()
        {
        }

        public static DownloadTask Create(UBundle bundle, IList<string> urls, int retry, string localPathRoot, Action<DownloadTask> callback)
        {
            var task = new DownloadTask();
            task._urls = urls;
            task._bundle = bundle;
            task._bundle.AddRef();
            task._callback = callback;

            task.retry = retry;
            task.tempPath = Path.Combine(localPathRoot, bundle.name + ".part");
            task.filePath = Path.Combine(localPathRoot, bundle.name);
            return task;
        }

        public void Run()
        {
            _running = true;
            ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadExec));
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

        private void DownloadExec(object state)
        {
            while (true)
            {
                FileStream file;
                var crc = new Utils.Crc16();
                var totalSize = this._bundle.size;
                var partialSize = 0;
                if (File.Exists(tempPath))
                {
                    var fileInfo = new FileInfo(tempPath);
                    file = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    partialSize = (int)fileInfo.Length;
                    if (partialSize == totalSize)
                    {
                        crc.Update(file);
                        if (crc.hex == _bundle.checksum)
                        {
                            file.Close();
                            this.Complete(null);
                            return;
                        }
                        file.SetLength(0);
                        partialSize = 0;
                    }
                    else if (partialSize > totalSize)
                    {
                        file.SetLength(0);
                        partialSize = 0;
                    }
                    else
                    {
                        crc.Update(file);
                        // file.Seek(partialSize, SeekOrigin.Begin);
                    }
                }
                else
                {
                    file = File.Open(tempPath, FileMode.Truncate, FileAccess.ReadWrite);
                }
                var uri = new Uri(this.url);
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Http.Get;
                req.ContentType = "application/octet-stream";
                if (partialSize > 0)
                {
                    req.AddRange(partialSize);
                }
                using (var rsp = req.GetResponse())
                {
                    using (var stream = rsp.GetResponseStream())
                    {
                        var buffer = new byte[512];
                        var recvAll = 0L;
                        while (recvAll < rsp.ContentLength)
                        {
                            var recv = stream.Read(buffer, 0, buffer.Length);
                            if (recv > 0)
                            {
                                recvAll += recv;
                                file.Write(buffer, 0, recv);
                                crc.Update(buffer, 0, recv);
                            }
                            else
                            {
                                throw new InvalidDataException();
                            }
                        }
                        file.Flush();
                        // check crc
                    }
                }
                // var req = UnityWebRequest.Get(url);
                // if (partialSize > 0)
                // {
                //     req.SetRequestHeader("Range", $"bytes={partialSize}-{totalSize}");
                // }
                // req.downloadHandler = new DownloadHandlerBuffer();
                // yield return req.SendWebRequest();
                // long contentLength;
                // if (!long.TryParse(req.GetResponseHeader("Content-Length"), out contentLength))
                // {
                //     if (!this.Retry())
                //     {
                //         this.Complete("too many retry");
                //         return;
                //     }
                //     continue;
                // }
                //TODO: download content ...
                // UnityWebRequest 可能没办法既控制buffer复用又支持续传, 还是要自己实现 
                throw new NotImplementedException();
            }
        }

        private bool CheckStream(FileStream stream)
        {
            var crc = new Utils.Crc16();
            stream.Seek(0, SeekOrigin.Begin);
            crc.Update(stream);
            if (crc.hex == this._bundle.checksum)
            {
                return true;
            }
            return false;
        }

        private void Complete(string error)
        {
            if (!_isDone)
            {
                _error = error;
                _isDone = true;
                _running = false;
                JobScheduler.DispatchMain(() =>
                {
                    this._bundle.RemoveRef();
                    _callback(this);
                });
            }
        }

        // return stream of downloaded file
        public Stream OpenFile()
        {
            return File.OpenRead(filePath);
        }
    }
}
