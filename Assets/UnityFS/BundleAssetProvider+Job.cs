using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace UnityFS
{
    using UnityEngine;

    public partial class BundleAssetProvider
    {
        // 保证所有指定级别的包文件均为本地最新状态
        public IList<DownloadWorker.JobInfo> EnsureBundles(Manifest.BundleLoad load, Action onComplete)
        {
            var jobs = new List<DownloadWorker.JobInfo>();
            var countdown = new Utils.CountdownObject(onComplete);
            for (int i = 0, size = _manifest.bundles.Count; i < size; i++)
            {
                var bundleInfo = _manifest.bundles[i];
                if ((bundleInfo.load & load) != 0)
                {
                    if (!_IsBundleValid(bundleInfo))
                    {
                        countdown.Add();
                        var job = _DownloadBundleFile(bundleInfo, () => countdown.Remove(), _bytesPerSecond);
                        if (job != null)
                        {
                            jobs.Add(job);
                        }
                    }
                }
            }

            countdown.Start();
            return jobs;
        }

        public DownloadWorker.JobInfo EnsureBundle(Manifest.BundleInfo bundleInfo)
        {
            if (!_IsBundleValid(bundleInfo))
            {
                return _DownloadBundleFile(bundleInfo, null, _bytesPerSecondIdle);
            }

            return null;
        }

        public IList<Manifest.BundleInfo> GetInvalidatedBundles()
        {
            var size = _manifest.bundles.Count;
            var list = new List<Manifest.BundleInfo>(size);
            for (var i = 0; i < size; i++)
            {
                var bundleInfo = _manifest.bundles[i];
                if (!_IsBundleValid(bundleInfo))
                {
                    list.Add(bundleInfo);
                }
            }

            return list;
        }

        private bool _IsBundleValid(Manifest.BundleInfo bundleInfo)
        {
            // 仅验证 StreamingAssets 清单内存在此资源包 (因为没办法直接安全有效地访问 StreamingAssets 内文件)
            if (_streamingAssets.Contains(bundleInfo))
            {
                return true;
            }

            var fullPath = Path.Combine(_localPathRoot, bundleInfo.name);
            if (Utils.Helpers.IsBundleFileValid(fullPath, bundleInfo))
            {
                return true;
            }

            return false;
        }

        public void ForEachTask(Action<ITask> callback)
        {
            for (var it = _tasks.First; it != null; it = it.Next)
            {
                callback(it.Value);
            }
        }

        // 下载包文件 (优先考虑从 StreamingAssets 载入, 无法载入时从网络下载)
        //NOTE: 调用此接口时已经确认本地包文件无效 (本地临时存储)
        private void DownloadBundleFile(Manifest.BundleInfo bundleInfo, Action callback)
        {
            var oldJob = _FindDownloadJob(bundleInfo.name);
            if (oldJob != null)
            {
                if (callback != null)
                {
                    oldJob.callback += callback;
                }

                return;
            }

            if (_streamingAssets.Contains(bundleInfo))
            {
                JobScheduler.DispatchCoroutine(
                    _streamingAssets.LoadStream(bundleInfo, stream =>
                    {
                        if (stream != null)
                        {
                            var bundle = TryGetBundle(bundleInfo);
                            if (bundle != null)
                            {
                                bundle.Load(Utils.Helpers.GetDecryptStream(stream, bundle.bundleInfo, _password));
                            }
                            else
                            {
                                stream.Close();
                            }

                            callback?.Invoke();
                        }
                        else
                        {
                            Debug.LogWarningFormat("read from streamingassets failed: {0}", bundleInfo.name);
                            _DownloadBundleFile(bundleInfo, callback, _bytesPerSecond);
                        }
                    })
                );
            }
            else
            {
                _DownloadBundleFile(bundleInfo, callback, _bytesPerSecond);
            }
        }

        private DownloadWorker.JobInfo _FindDownloadJob(string bundleName)
        {
            for (var it = _tasks.First; it != null; it = it.Next)
            {
                var oldJob = it.Value;
                if (oldJob.bundleInfo.name == bundleName)
                {
                    return oldJob;
                }
            }

            return null;
        }

        //NOTE: 调用此接口时已经确认 StreamingAssets 以及本地包文件均无效
        private DownloadWorker.JobInfo _DownloadBundleFile(Manifest.BundleInfo bundleInfo, Action callback, int bytesPerSecond)
        {
            var oldJob = _FindDownloadJob(bundleInfo.name);
            if (oldJob != null)
            {
                oldJob.bytesPerSecond = _bytesPerSecond;
                if (callback != null)
                {
                    oldJob.callback += callback;
                }

                return oldJob;
            }

            // 无法打开现有文件, 下载新文件
            var bundlePath = Path.Combine(_localPathRoot, bundleInfo.name);
            var newJob = new DownloadWorker.JobInfo()
            {
                bytesPerSecond = bytesPerSecond,
                bundleInfo = bundleInfo,
                finalPath = bundlePath,
                callback = callback
            };
            AddDownloadTask(newJob);
            Schedule();
            return newJob;
        }

        private void onDownloadJobDone(DownloadWorker.JobInfo jobInfo)
        {
            jobInfo.isDone = true;
            jobInfo.isRunning = false;
            _activeJobs--;
            _tasks.Remove(jobInfo);
            jobInfo.callback?.Invoke();
            ResourceManager.GetListener().OnTaskComplete(jobInfo);
            var bundle = TryGetBundle(jobInfo.bundleInfo);
            if (bundle != null)
            {
                if (!LoadBundleFile(bundle))
                {
                    bundle.Load(null);
                }
            }

            Schedule();
        }

        private DownloadWorker.JobInfo AddDownloadTask(DownloadWorker.JobInfo newTask)
        {
            for (var it = _tasks.First; it != null; it = it.Next)
            {
                var task = it.Value;

                if (newTask.bundleInfo.priority > task.bundleInfo.priority)
                {
                    _tasks.AddBefore(it, newTask);
                    return newTask;
                }
            }

            _tasks.AddLast(newTask);
            return newTask;
        }

        private void Schedule()
        {
            if (_activeJobs == 0)
            {
                var taskNode = _tasks.First;
                if (taskNode != null)
                {
                    var task = taskNode.Value;

                    _activeJobs++;
                    task.isRunning = true;
                    _worker.AddJob(task);
                    ResourceManager.GetListener().OnTaskStart(task);
                }
                else
                {
                    /*
                     * TODO: 空闲下载
                     * if (idle download is on) {
                     *     check if any idle job available
                     *     generate a job and add it to idle worker
                     * }
                     */
                }
            }
        }
    }
}