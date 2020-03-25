using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UnityFS
{
    using UnityEngine;

    public class DownloadWorker
    {
        public const string BundleContentType = "application/octet-stream";
        public const string PartExt = ".part";

        public class JobInfo : ITask
        {
            public Manifest.BundleInfo bundleInfo;
            public int retry; // 重试次数 (<=0 时无限重试)
            public int tried; // 已重试次数
            public int bytes; // 当前字节数
            public string finalPath; // 最终存储路径
            public string error; // 错误
            public Action callback;

            public bool isRunning { get; set; }
            public bool isDone { get; set; }
            public float progress => Mathf.Clamp01((float) bytes / bundleInfo.size);
            public int size => bundleInfo.size;

            public int priority => bundleInfo.priority;
            public string name => bundleInfo.name;
            public string path => finalPath;
        }

        private static bool _destroy;
        private byte[] _buffer;
        private int _timeout = 10 * 1000; // http 请求超时时间 (毫秒)
        private Utils.Crc16 _crc = new Utils.Crc16();
        private int _bpms = 1024 * 712 / 10; // 712KB/S
        private LinkedList<JobInfo> _jobInfos = new LinkedList<JobInfo>();
        private Thread _thread;
        private AutoResetEvent _event = new AutoResetEvent(false);
        private FileStream _fileStream;

        // invoke in main thread
        private Action<JobInfo> _callback;

        public DownloadWorker(Action<JobInfo> callback, int bufferSize, int bps,
            System.Threading.ThreadPriority threadPriority)
        {
            _bpms = bps / 10;
            _callback = callback;
            _buffer = new byte[bufferSize];
            _thread = new Thread(_Run)
            {
                Name = "DownloadWorker",
                Priority = threadPriority,
                IsBackground = true
            };
            _thread.Start();
        }

        public void Abort()
        {
            _destroy = true;
            lock (_jobInfos)
            {
                _jobInfos.Clear();
            }

            if (_thread != null)
            {
                try
                {
                    _thread.Abort();
                    _thread = null;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            if (_fileStream != null)
            {
                try
                {
                    _fileStream.Close();
                    _fileStream = null;
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public bool AddJob(JobInfo jobInfo)
        {
            lock (_jobInfos)
            {
                if (_destroy)
                {
                    return false;
                }

                _jobInfos.AddLast(jobInfo);
            }

            _event.Set();
            return true;
        }

        private string GetUrl(JobInfo jobInfo)
        {
            var urls = ResourceManager.urls;
            var url = urls[jobInfo.tried % urls.Count];

            if (url.EndsWith("/"))
            {
                url += jobInfo.bundleInfo.name;
            }
            else
            {
                url += "/" + jobInfo.bundleInfo.name;
            }

            url += "?checksum=" + (jobInfo.bundleInfo.checksum ?? DateTime.Now.Ticks.ToString());
            return url;
        }

        private void _Run()
        {
            while (!_destroy)
            {
                try
                {
                    ProcessJob(GetJob());
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("fatal error: {0}", exception);
                }
                finally
                {
                    if (_fileStream != null)
                    {
                        _fileStream.Close();
                        _fileStream = null;
                    }
                }
            }
        }

        private JobInfo GetJob()
        {
            _event.WaitOne();
            lock (_jobInfos)
            {
                var first = _jobInfos.First.Value;
                _jobInfos.RemoveFirst();
                return first;
            }
        }

        private void ProcessJob(JobInfo jobInfo)
        {
            Debug.LogFormat("processing job: {0} ({1})", jobInfo.name, jobInfo.bundleInfo.comment);
            var tempPath = jobInfo.finalPath + PartExt;
            if (_fileStream != null)
            {
                _fileStream.Close();
                _fileStream = null;
            }

            while (true)
            {
                string error = null;
                var partialSize = 0;
                var success = true;
                _crc.Clear();
                if (_fileStream == null)
                {
                    try
                    {
                        var fileInfo = new FileInfo(tempPath);
                        if (fileInfo.Exists) // 处理续传
                        {
                            _fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            partialSize = (int) fileInfo.Length;
                            if (partialSize > jobInfo.bundleInfo.size) // 目标文件超过期望大小, 直接废弃
                            {
                                _fileStream.SetLength(0);
                                partialSize = 0;
                            }
                            else if (partialSize <= jobInfo.bundleInfo.size) // 续传
                            {
                                _crc.Update(_fileStream);
                                Debug.LogFormat("partial check {0} && {1} ({2})", partialSize, jobInfo.bundleInfo.size,
                                    _crc.hex);
                            }
                        }
                        else // 创建下载文件
                        {
                            if (!Directory.Exists(fileInfo.DirectoryName))
                            {
                                Directory.CreateDirectory(fileInfo.DirectoryName);
                            }

                            _fileStream = File.Open(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                            _fileStream.SetLength(0);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogErrorFormat("file exception: {0}\n{1}", jobInfo.finalPath, exception);
                        error = $"file exception: {exception}";
                        success = false;
                    }
                }
                else
                {
                    _fileStream.SetLength(0L);
                }

                if (success && (jobInfo.bundleInfo.size <= 0 || partialSize < jobInfo.bundleInfo.size))
                {
                    var url = GetUrl(jobInfo);
                    try
                    {
                        var uri = new Uri(url);
                        var req = WebRequest.CreateHttp(uri);
                        req.Method = WebRequestMethods.Http.Get;
                        req.ContentType = BundleContentType;
                        req.ReadWriteTimeout = 10000;
                        if (_timeout > 0)
                        {
                            req.Timeout = _timeout;
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
                                var recvCalc = 0L;
                                var stopwatch = new Stopwatch();
                                stopwatch.Start();
                                while (recvAll < rsp.ContentLength)
                                {
                                    var recv = webStream.Read(_buffer, 0, Math.Min(_bpms, _buffer.Length));
                                    if (recv > 0 && !_destroy)
                                    {
                                        recvCalc += recv;
                                        if (recvCalc >= _bpms)
                                        {
                                            var millisecs = stopwatch.ElapsedMilliseconds;
                                            var delay = (int)(100.0 * recvCalc / _bpms - millisecs);
                                            // Debug.LogFormat("net ++ {0} {1} sbps {2} recv {3}", delay, millisecs, _bpms, recvCalc);
                                            if (delay > 0)
                                            {
                                                Thread.Sleep(delay);
                                            }
                                            stopwatch.Restart();
                                            recvCalc -= _bpms;
                                        }
                                        recvAll += recv;
                                        _fileStream.Write(_buffer, 0, recv);
                                        _crc.Update(_buffer, 0, recv);
                                        jobInfo.bytes = (int) (recvAll + partialSize);
                                        // PrintDebug($"{recvAll + partialSize}, {_size}, {_progress}");
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogErrorFormat("network exception: {0}\n{1}", url, exception);
                        error = $"network exception: {exception}";
                        success = false;
                    }
                }

                if (success && _fileStream.Length != jobInfo.bundleInfo.size)
                {
                    if (jobInfo.bundleInfo.size > 0)
                    {
                        error = string.Format("filesize exception: {0} {1} != {2}", jobInfo.bundleInfo.name,
                            _fileStream.Length, jobInfo.bundleInfo.size);
                        Debug.LogError(error);
                        success = false;
                    }
                    else
                    {
                        jobInfo.bundleInfo.size = (int) _fileStream.Length;
                    }
                }

                if (success && _crc.hex != jobInfo.bundleInfo.checksum)
                {
                    if (jobInfo.bundleInfo.checksum != null)
                    {
                        error = string.Format("corrupted file: {0} {1} != {2}", jobInfo.bundleInfo.name, _crc.hex,
                            jobInfo.bundleInfo.checksum);
                        Debug.LogError(error);
                        success = false;
                    }
                    else
                    {
                        jobInfo.bundleInfo.checksum = _crc.hex;
                    }
                }

                if (_destroy)
                {
                    success = false;
                }

                if (success)
                {
                    try
                    {
                        // _WriteStream(buffer, fileStream, finalPath);
                        _fileStream.Close();
                        _fileStream = null;
                        if (File.Exists(jobInfo.finalPath))
                        {
                            File.Delete(jobInfo.finalPath);
                        }

                        File.Move(tempPath, jobInfo.finalPath);
                        // 写入额外的 meta
                        var meta = new Metadata()
                        {
                            checksum = jobInfo.bundleInfo.checksum,
                            size = jobInfo.bundleInfo.size,
                        };
                        var json = JsonUtility.ToJson(meta);
                        var metaPath = jobInfo.finalPath + Metadata.Ext;
                        File.WriteAllText(metaPath, json);
                        Complete(jobInfo);
                        break;
                    }
                    catch (Exception exception)
                    {
                        error = string.Format("write exception: {0}\n{1}", jobInfo.bundleInfo.name, exception);
                        Debug.LogError(error);
                        success = false;
                    }
                }

                jobInfo.tried++;
                if (jobInfo.retry > 0 && jobInfo.tried >= jobInfo.retry)
                {
                    if (_fileStream != null)
                    {
                        _fileStream.Close();
                        _fileStream = null;
                    }

                    jobInfo.error = error ?? "unknown error";
                    Complete(jobInfo);
                    break;
                }

                Thread.Sleep(2000);
                Debug.LogErrorFormat("[retry] download failed: {0}\n{1}", jobInfo.bundleInfo.name, error);
            }
        }

        public static void Destroy()
        {
            _destroy = true;
        }

        private void Complete(JobInfo jobInfo)
        {
            if (_callback != null)
            {
                JobScheduler.DispatchMain(() => { _callback(jobInfo); });
            }
        }
    }
}