using System;
using System.IO;
using System.Collections.Generic;

namespace Examples
{
    using UnityEngine;

    public class DownloadTest : MonoBehaviour
    {
        void Awake()
        {
            UnityFS.JobScheduler.Initialize();
            UnityFS.ResourceManager.urls = UnityFS.Utils.Helpers.URLs(
                "http://localhost:8080/"
            );
            var task = UnityFS.DownloadTask.Create(
                "Assets/Examples/Files/test1.json",
                "713c", // hash check
                345, // size check
                0,
                "E:/Assets/Examples/Files/test1.pkg",
                3,  // retry
                10, // timeout
                self =>
            {
                Debug.Log($"complete {self.error}");
            });
            task.SetDebugMode(true);
            task.Run();
        }
    }
}
