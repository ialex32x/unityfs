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
        public void EnsureBundles(Manifest.BundleLoad load, Action onComplete)
        {
            var countdown = new Utils.CountdownObject(onComplete);
            for (int i = 0, size = _manifest.bundles.Count; i < size; i++)
            {
                var bundleInfo = _manifest.bundles[i];
                if ((bundleInfo.load & load) != 0)
                {
                    var fullPath = Path.Combine(_localPathRoot, bundleInfo.name);
                    if (!Utils.Helpers.IsBundleFileValid(fullPath, bundleInfo))
                    {
                        countdown.Add();
                        DownloadBundleFile(bundleInfo, () => countdown.Remove());
                    }
                }
            }

            countdown.Start();
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
            if (_streamingAssets.Contains(bundleInfo))
            {
                JobScheduler.DispatchCoroutine(
                    _streamingAssets.LoadStream(bundleInfo, stream =>
                    {
                        var bundle = TryGetBundle(bundleInfo);
                        if (bundle != null)
                        {
                            if (stream != null)
                            {
                                bundle.Load(Utils.Helpers.GetDecryptStream(stream, bundle.bundleInfo, _password));
                                callback?.Invoke();
                            }
                            else
                            {
                                Debug.LogWarningFormat("read from streamingassets failed: {0}", bundleInfo.name);
                                _DownloadBundleFile(bundleInfo, callback);
                            }
                        }
                    })
                );
            }
            else
            {
                _DownloadBundleFile(bundleInfo, callback);
            }
        }

        //NOTE: 调用此接口时已经确认 StreamingAssets 以及本地包文件均无效
        private bool _DownloadBundleFile(Manifest.BundleInfo bundleInfo, Action callback)
        {
            for (var it = _tasks.First; it != null; it = it.Next)
            {
                var oldJob = it.Value;
                if (oldJob.bundleInfo.name == bundleInfo.name)
                {
                    if (callback != null)
                    {
                        oldJob.callback += callback;
                    }

                    return false;
                }
            }

            // 无法打开现有文件, 下载新文件
            var bundlePath = Path.Combine(_localPathRoot, bundleInfo.name);
            var newJob = new DownloadWorker.JobInfo()
            {
                bundleInfo = bundleInfo,
                finalPath = bundlePath,
                callback = callback
            };
            AddDownloadTask(newJob);
            Schedule();
            return true;
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
                    _downloadWorker.AddJob(task);
                    ResourceManager.GetListener().OnTaskStart(task);
                }
            }
        }
    }
}