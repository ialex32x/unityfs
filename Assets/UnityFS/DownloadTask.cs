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
        public const string BundleContentType = "application/octet-stream";
        public const string PartExt = ".part";
        public const int BufferSize = 1024 * 2;

        private int _retry;       // 重试次数 (<0 时无限重试)
        private string _rootPath; // 目录路径

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

        private void PrintError(string message)
        {
            // Debug.LogError(message);
        }

        private void DownloadExec(object state)
        {
            var finalPath = Path.Combine(_rootPath, _name);
            var tempPath = finalPath + PartExt;
            var metaPath = finalPath + Metadata.Ext;
            var totalSize = _size;
            FileStream fileStream = null;
            while (true)
            {
                string error = null;
                var buffer = new byte[BufferSize];
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
                            if (partialSize > totalSize) // 目标文件超过期望大小, 直接废弃
                            {
                                fileStream.SetLength(0);
                                partialSize = 0;
                            }
                            else if (partialSize <= totalSize) // 续传
                            {
                                crc.Update(fileStream);
                                PrintError($"partial check {partialSize} && {totalSize} ({crc.hex})");
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
                        PrintError($"file exception: {exception}");
                        error = exception.ToString();
                        success = false;
                    }
                }
                else
                {
                    fileStream.SetLength(0L);
                }

                if (success && partialSize < totalSize)
                {
                    try
                    {
                        _HttpDownload(this.url, partialSize, buffer, crc, fileStream);
                    }
                    catch (Exception exception)
                    {
                        PrintError($"network exception: {exception}");
                        error = exception.ToString();
                        success = false;
                    }
                }

                if (success && fileStream.Length != _size)
                {
                    PrintError($"filesize exception: {fileStream.Length} != {_size}");
                    error = "wrong file size";
                    success = false;
                }
                else if (success && crc.hex != _checksum)
                {
                    PrintError($"checksum exception: {crc.hex} != {_checksum}");
                    error = "corrupted file";
                    success = false;
                }

                if (success)
                {
                    try
                    {
                        // _WriteStream(buffer, fileStream, finalPath);
                        fileStream.Close();
                        fileStream = null;
                        File.Copy(tempPath, finalPath, true);
                        _WriteMetadata(metaPath);
                        File.Delete(tempPath);
                        Complete(null);
                        break;
                    }
                    catch (Exception exception)
                    {
                        PrintError($"write exception: {exception}");
                        error = exception.ToString();
                        success = false;
                    }
                }

                if (!Retry())
                {
                    Complete(error ?? "unknown error");
                    break;
                }
                Thread.Sleep(100);
                PrintError($"[retry] download failed ({error})");
            }
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

        private void _HttpDownload(string url, int partialSize, byte[] buffer, Utils.Crc16 crc, Stream targetStream)
        {
            var uri = new Uri(url);
            var req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = WebRequestMethods.Http.Get;
            req.ContentType = BundleContentType;
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
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
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
    }
}
