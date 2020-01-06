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

    public class DownloadTask : ITask
    {
        public const string BundleContentType = "application/octet-stream";
        public const string PartExt = ".part";
        public const int BufferSize = 1024 * 2;

        private static bool _destroy = false;

        private bool _debug;
        private int _retry;        // 重试次数 (<0 时无限重试)
        private string _finalPath; // 最终存储路径

        private string _name;
        private string _checksum;
        private int _size;
        private int _priority;
        private float _progress;
        private int _timeout; // in seconds

        private bool _running;
        private int _urlIndex;
        private string _url;
        private IList<string> _urls;

        private bool _isDone;
        private string _error;

        // invoke in main thread
        private Action<DownloadTask> _callback;

        public string name
        {
            get { return _name; }
        }

        public int priority
        {
            get { return _priority; }
        }

        public float progress
        {
            get { return _progress; }
        }

        public string checksum
        {
            get { return _checksum; }
        }

        public int size
        {
            get { return _size; }
        }

        // 运行中
        public bool isRunning
        {
            get { return _running; }
        }

        // 是否已完成 
        public bool isDone
        {
            get
            {
                lock (this)
                {
                    return _isDone;
                }
            }
        }

        public string path
        {
            get { return _finalPath; }
        }

        // 错误信息 (null 表示没有错误)
        public string error
        {
            get { return _error; }
        }

        // 当前请求的url
        public string url
        {
            get { return _url; }
        }

        private DownloadTask()
        {
        }

        public static DownloadTask Create(
            Manifest.BundleInfo bundleInfo,
            IList<string> urls,
            string filePathRoot,
            int retry,
            int timeout,
            Action<DownloadTask> callback)
        {
            return Create(bundleInfo.name, bundleInfo.checksum, bundleInfo.size, bundleInfo.priority, urls, filePathRoot, retry, timeout, callback);
        }

        public static DownloadTask Create(
            string name, string checksum, int size,
            int priority,
            IList<string> urls,
            string filePathRoot,
            int retry,
            int timeout,
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
            task._timeout = timeout;
            task._finalPath = Path.Combine(filePathRoot, name);
            task.SetUrl();
            return task;
        }

        public void Run()
        {
            _running = true;
            ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadExec));
        }

        private void SetUrl()
        {
            if (_urls[_urlIndex].EndsWith("/"))
            {
                _url = _urls[_urlIndex] + _name;
            }
            else
            {
                _url = _urls[_urlIndex] + "/" + _name;
            }
            _url += "?checksum=" + (_checksum ?? DateTime.Now.Ticks.ToString());
        }

        private bool Retry(int retry)
        {
            lock (this)
            {
                if (_isDone || _destroy)
                {
                    return false;
                }
            }
            if (_retry > 0 && retry >= _retry)
            {
                return false;
            }
            if (_urlIndex < _urls.Count - 1)
            {
                ++_urlIndex;
                SetUrl();
            }
            return true;
        }

        public DownloadTask SetDebugMode(bool debug)
        {
            _debug = debug;
            return this;
        }

        private void PrintError(string message)
        {
            if (_debug)
            {
                Debug.LogError(message);
            }
        }

        private void PrintDebug(string message)
        {
            if (_debug)
            {
                Debug.Log($"[task:{_name}] {message}");
            }
        }

        private void DownloadExec(object state)
        {
            var buffer = new byte[BufferSize];
            var tempPath = _finalPath + PartExt;
            var metaPath = _finalPath + Metadata.Ext;
            var retry = 0;
            FileStream fileStream = null;

            while (true)
            {
                string error = null;
                var crc = new Utils.Crc16();
                var partialSize = 0;
                var success = true;
                if (fileStream == null)
                {
                    try
                    {
                        if (File.Exists(tempPath)) // 处理续传
                        {
                            var fileInfo = new FileInfo(tempPath);
                            fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            partialSize = (int)fileInfo.Length;
                            if (partialSize > _size) // 目标文件超过期望大小, 直接废弃
                            {
                                fileStream.SetLength(0);
                                partialSize = 0;
                            }
                            else if (partialSize <= _size) // 续传
                            {
                                crc.Update(fileStream);
                                PrintDebug($"partial check {partialSize} && {_size} ({crc.hex})");
                            }
                        }
                        else // 创建下载文件
                        {
                            fileStream = File.Open(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            fileStream.SetLength(0);
                        }
                    }
                    catch (Exception exception)
                    {
                        // PrintError($"file exception: {exception}");
                        error = $"file exception: {exception}";
                        success = false;
                    }
                }
                else
                {
                    fileStream.SetLength(0L);
                }

                if (success && (_size <= 0 || partialSize < _size))
                {
                    try
                    {
                        _HttpDownload(this.url, partialSize, buffer, crc, fileStream, _timeout);
                    }
                    catch (Exception exception)
                    {
                        // PrintError($"network exception: {exception}");
                        error = $"network exception: {exception}";
                        success = false;
                    }
                }

                if (success && fileStream.Length != _size)
                {
                    if (_size > 0)
                    {
                        // PrintError($"filesize exception: {fileStream.Length} != {_size}");
                        error = $"wrong file size: {fileStream.Length} != {_size}";
                        success = false;
                    }
                    else
                    {
                        _size = (int)fileStream.Length;
                    }
                }
                else if (success && crc.hex != _checksum)
                {
                    if (_checksum != null)
                    {
                        // PrintError($"checksum exception: {crc.hex} != {_checksum}");
                        error = $"corrupted file: {crc.hex} != {_checksum}";
                        success = false;
                    }
                    else
                    {
                        _checksum = crc.hex;
                    }
                }

                lock (this)
                {
                    if (_isDone || _destroy)
                    {
                        success = false;
                    }
                }

                if (success)
                {
                    try
                    {
                        // _WriteStream(buffer, fileStream, finalPath);
                        fileStream.Close();
                        fileStream = null;
                        if (File.Exists(_finalPath))
                        {
                            File.Delete(_finalPath);
                        }
                        File.Move(tempPath, _finalPath);
                        _WriteMetadata(metaPath);
                        Complete(null);
                        // PrintDebug("download succeeded");
                        break;
                    }
                    catch (Exception exception)
                    {
                        // PrintError($"write exception: {exception}");
                        error = $"write exception: {exception}";
                        success = false;
                    }
                }

                if (!Retry(++retry))
                {
                    if (fileStream != null)
                    {
                        fileStream.Close();
                        fileStream = null;
                    }
                    Complete(error ?? "unknown error");
                    PrintError($"[stop] download failed ({error})");
                    break;
                }
                Thread.Sleep(1000);
                PrintError($"[retry] ({_destroy}) download failed ({error})");
            }
            PrintDebug("download task thread exited");
        }

        private void _WriteMetadata(string metaPath)
        {
            // 写入额外的 meta
            var meta = new Metadata()
            {
                checksum = _checksum,
                size = _size,
            };
            var json = JsonUtility.ToJson(meta);
            File.WriteAllText(metaPath, json);
        }

        private void _HttpDownload(string url, int partialSize, byte[] buffer, Utils.Crc16 crc, Stream targetStream, int timeout)
        {
            PrintDebug($"downloading from {url}");
            var uri = new Uri(url);
            var req = WebRequest.CreateHttp(uri);
            req.Method = WebRequestMethods.Http.Get;
            req.ContentType = BundleContentType;
            req.ReadWriteTimeout = 10000;
            if (timeout > 0)
            {
                req.Timeout = timeout * 1000;
            }
            if (partialSize > 0)
            {
                req.AddRange(partialSize);
            }
            using (var rsp = req.GetResponse())
            {
                using (var webStream = rsp.GetResponseStream())
                {
                    var recvAll = 0L;
                    while (recvAll < rsp.ContentLength)
                    {
                        var recv = webStream.Read(buffer, 0, buffer.Length);
                        if (recv > 0)
                        {
                            recvAll += recv;
                            targetStream.Write(buffer, 0, recv);
                            crc.Update(buffer, 0, recv);
                            _progress = Mathf.Clamp01((float)(recvAll + partialSize) / _size);
                            // Thread.Sleep(200); // 模拟低速下载
                            // PrintDebug($"{recvAll + partialSize}, {_size}, {_progress}");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            // PrintDebug($"download exited");
        }

        public static void Destroy()
        {
            _destroy = true;
            // Debug.Log("destroy");
        }

        public void Abort()
        {
            lock (this)
            {
                if (!_isDone)
                {
                    _error = "aborted";
                    _isDone = true;
                    _running = false;
                }
            }
        }

        private void Complete(string error)
        {
            lock (this)
            {
                if (!_isDone)
                {
                    _error = error;
                    _isDone = true;
                    _running = false;
                    if (_callback != null)
                    {
                        var cb = _callback;
                        _callback = null;
                        JobScheduler.DispatchMainAnyway(() =>
                        {
                            cb(this);
                        });
                    }
                }
            }
        }
    }
}
