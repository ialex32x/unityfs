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

    // public class TaskManager
    // {
    //     private int _slow = 0;
    //     private int _bufferSize = 0;
    //     private int _runningTasks = 0;
    //     private int _concurrentTasks = 0;
    //     private LinkedList<ITask> _tasks = new LinkedList<ITask>();

    //     public TaskManager(int concurrentTasks, int slow, int bufferSize)
    //     {
    //         _slow = slow;
    //         _bufferSize = bufferSize;
    //         _concurrentTasks = Math.Max(1, Math.Min(concurrentTasks, 4)); // 并发下载任务数量 
    //     }

    //     public void ForEachTask(Action<ITask> callback)
    //     {
    //         for (var node = _tasks.First; node != null; node = node.Next)
    //         {
    //             var task = node.Value;
    //             callback(task);
    //         }
    //     }

    //     private ITask AddDownloadTask(ITask newTask, bool bSchedule)
    //     {
    //         for (var node = _tasks.First; node != null; node = node.Next)
    //         {
    //             var task = node.Value;
    //             if (!task.isRunning && !task.isDone)
    //             {
    //                 if (newTask.priority > task.priority)
    //                 {
    //                     _tasks.AddAfter(node, newTask);
    //                     Schedule();
    //                     return newTask;
    //                 }
    //             }
    //         }
    //         _tasks.AddLast(newTask);
    //         if (bSchedule)
    //         {
    //             Schedule();
    //         }
    //         return newTask;
    //     }

    //     private void RemoveDownloadTask(ITask task)
    //     {
    //         _tasks.Remove(task);
    //         _runningTasks--;
    //         ResourceManager.GetListener().OnTaskComplete(task);
    //         Schedule();
    //     }

    //     private void Schedule()
    //     {
    //         if (_runningTasks >= _concurrentTasks)
    //         {
    //             return;
    //         }

    //         for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
    //         {
    //             var task = taskNode.Value;
    //             if (!task.isRunning && !task.isDone)
    //             {
    //                 _runningTasks++;
    //                 task.Run();
    //                 ResourceManager.GetListener().OnTaskStart(task);
    //                 break;
    //             }
    //         }
    //     }

    //     // 终止所有任务
    //     public void Abort()
    //     {
    //         for (var taskNode = _tasks.First; taskNode != null; taskNode = taskNode.Next)
    //         {
    //             var task = taskNode.Value;
    //             task.Abort();
    //         }
    //         _tasks.Clear();
    //     }
    // }
}
