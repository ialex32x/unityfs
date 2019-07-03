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
            var task = UnityFS.DownloadTask.Create("bundle_19.pkg", "96f1", 104437, 0, urls, 1, "D:\\", self =>
            {
                Debug.Log($"complete {self.error}");
            });
            task.Run();
        }
    }
}
