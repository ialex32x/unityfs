using System;
using UnityEngine;

namespace UnityFS
{
    public interface ILogger
    {
        void OnWorkerError(DownloadWorker worker, Exception exception);
        void OnTaskError(ITask task, string error);
        void OnTaskError(ITask task, Exception exception);
    }

    public class DefaultLogger : ILogger
    {
        public void OnWorkerError(DownloadWorker worker, Exception exception)
        {
            Debug.LogErrorFormat("[Worker] {0}\n{1}", exception.Message, exception.StackTrace);
        }
        
        public void OnTaskError(ITask task, string error)
        {
            Debug.LogErrorFormat("{0}: {1}", task.name, error);
        }
        
        public void OnTaskError(ITask task,  Exception exception)
        {
            Debug.LogErrorFormat("{0}: {1}\n{2}", task.name, exception.Message, exception.StackTrace);
        }
    }
}