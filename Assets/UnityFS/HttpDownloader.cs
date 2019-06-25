using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;
    using UnityEngine.Networking;

    public class HttpDownloader : IDownloader
    {
        // public const int BufferSize = 1024 * 32;
        // private byte[] _buffer = new byte[BufferSize];
        private string _outputPath;
        private List<Coroutine> _running = new List<Coroutine>();

        public HttpDownloader(string outputPath)
        {
            _outputPath = outputPath;
        }

        public void AddDownloadTask(DownloadTask task)
        {
            var co = JobScheduler.DispatchCoroutine(DownloadExec(task));
            _running.Add(co);
        }

        private IEnumerator DownloadExec(DownloadTask task)
        {
            while (true)
            {
                var url = task.url;
                var req = UnityWebRequest.Get(url);
                var partialSize = 0;
                var totalSize = task.size;
                req.SetRequestHeader("Range", $"bytes={partialSize}-{totalSize}");
                yield return req.SendWebRequest();
                long contentLength;
                if (!long.TryParse(req.GetResponseHeader("Content-Length"), out contentLength))
                {
                    if (!task.Retry())
                    {
                        task.Complete("too many retry");
                        yield break;
                    }
                    continue;
                }
                //TODO: ...
            }
        }
    }
}
