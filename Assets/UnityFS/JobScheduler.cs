using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class JobScheduler : MonoBehaviour
    {
        private static int _mainThreadId;
        private static JobScheduler _mb;

        public static void Initialize()
        {
            if (_mb == null)
            {
                _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var go = new GameObject("_ResourceManager");
                go.hideFlags = HideFlags.HideInHierarchy;
                _mb = go.AddComponent<JobScheduler>();
                _mb.StartCoroutine(_Update());
            }
        }

        private static List<Action> _actions = new List<Action>();

        private static IEnumerator _Update()
        {
            while (true)
            {
                lock (_actions)
                {
                    for (int i = 0, size = _actions.Count; i < size; i++)
                    {
                        _actions[i]();
                    }
                    _actions.Clear();
                }
                yield return null;
            }
        }

        public static Coroutine DispatchCoroutine(IEnumerator co)
        {
            return _mb.StartCoroutine(co);
        }

        public static void DispatchMainAnyway(Action action)
        {
            // Debug.Assert(_mb != null);
            lock (_actions)
            {
                _actions.Add(action);
            }
        }

        public static void DispatchMain(Action action)
        {
            // Debug.Assert(_mb != null);
            if (_mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId)
            {
                action();
                return;
            }
            lock (_actions)
            {
                _actions.Add(action);
            }
        }

        void OnDestroy()
        {
            ResourceManager.Close();
            DownloadTask.Destroy();
        }
    }
}
