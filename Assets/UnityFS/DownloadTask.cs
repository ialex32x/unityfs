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
        private int _retry;       // 重试次数 (<0 时无限重试)
        private string _rootPath; // 目录路径
        private string _tempPath; // 临时文件路径
        private FileStream _fileStream = null;

        private string _name;
        private string _checksum;
        private int _size;
        private int _priority;

        private bool _running;
        private int _urlIndex;
        private IList<string> _urls;

        private bool _isDone;
        private string _error;

        // invoke in main thread
        private Action<DownloadTask> _callback;
        private Action<DownloadTask> _prepare;

        public int priority
        {
            get { return _priority; }
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
                    return _urls[_urlIndex] + _name;
                }
                return _urls[_urlIndex] + "/" + _name;
            }
        }

        private DownloadTask()
        {
        }

        public static DownloadTask Create(
            string name, string checksum, int size,
            int priority,
            IList<string> urls,
            int retry,
            string localPathRoot,
            Action<DownloadTask> callback)
        {
            var task = new DownloadTask();
            task._urls = urls;
            task._callback = callback;
            task._name = name;
            task._checksum = checksum;
            task._size = size;
            task._priority = priority;
            task._retry = retry;
            task._rootPath = localPathRoot;
            task._tempPath = Path.Combine(localPathRoot, task._name + ".part");
            return task;
        }

        public void Run()
        {
            _running = true;
            ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadExec));
        }

        private bool Retry()
        {
            if (_retry > 0 && (_urlIndex + 1) >= _retry)
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
                var crc = new Utils.Crc16();
                var totalSize = _size;
                var partialSize = 0;
                if (_fileStream == null)
                {
                    if (File.Exists(_tempPath))
                    {
                        var fileInfo = new FileInfo(_tempPath);
                        _fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        partialSize = (int)fileInfo.Length;
                        if (partialSize == totalSize)
                        {
                            crc.Update(_fileStream);
                            if (crc.hex == _checksum)
                            {
                                this.Complete(null);
                                return;
                            }
                            _fileStream.SetLength(0);
                            partialSize = 0;
                        }
                        else if (partialSize > totalSize)
                        {
                            _fileStream.SetLength(0);
                            partialSize = 0;
                        }
                        else
                        {
                            crc.Update(_fileStream);
                            // file.Seek(partialSize, SeekOrigin.Begin);
                        }
                    }
                    else
                    {
                        _fileStream = File.Open(_tempPath, FileMode.Truncate, FileAccess.ReadWrite);
                    }
                }
                else
                {
                    _fileStream.SetLength(0L);
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
                                _fileStream.Write(buffer, 0, recv);
                                crc.Update(buffer, 0, recv);
                            }
                            else
                            {
                                throw new InvalidDataException();
                            }
                        }
                        _fileStream.Flush();
                        // check crc
                    }
                }
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
            if (crc.hex == this._checksum)
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
                    _callback(this);
                });
            }
        }

        // return stream of downloaded file
        public Stream GetStream()
        {
            return _fileStream;
        }
    }
}
