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
            var urls = UnityFS.Utils.Helpers.URLs(
                    "http://localhost:8080/"
                );
            var task = UnityFS.DownloadTask.Create(
                "bundle_19.pkg",
                "96f1", // hash check
                104437, // size check
                0,
                urls,
                "D:\\",
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
