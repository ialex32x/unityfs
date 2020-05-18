using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class JobScheduler : MonoBehaviour
    {
        private static int _mainThreadId;
        private static JobScheduler _mb;
        private static LinkedList<Action> _backlist = new LinkedList<Action>();

        public static void Initialize()
        {
            if (_mb == null)
            {
                _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var go = new GameObject("_ResourceManager");
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
                _mb = go.AddComponent<JobScheduler>();
                // _mb.StartCoroutine(_Update());
            }
            else
            {
                Clear();
            }
        }

        private void Update()
        {
            if (_backlist.Count != 0)
            {
                List<Action> list = null;
                lock (_backlist)
                {
                    if (_backlist.Count != 0)
                    {
                        list = new List<Action>(_backlist);
                        _backlist.Clear();
                    }
                }

                if (list != null)
                {
                    for (int i = 0, count = list.Count; i < count; i++)
                    {
                        var action = list[i];
                        action();
                    }
                }
            }
        }

        // main thread only
        public static Coroutine DispatchCoroutine(IEnumerator co)
        {
            return _mb != null ? _mb.StartCoroutine(co) : null;
        }

        private static IEnumerator _AfterSeconds(Action action, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        // main thread only
        public static void DispatchAfter(Action action, float seconds)
        {
            if (_mb != null)
            {
                _mb.StartCoroutine(_AfterSeconds(action, seconds));
            }
        }

        public static void DispatchMainAfter(Action action, float seconds)
        {
            DispatchMain(() => _mb.StartCoroutine(_AfterSeconds(action, seconds)));
        }

        public static void DispatchMain(Action action)
        {
            // Debug.Assert(_mb != null);
            if (_mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId)
            {
                action();
                return;
            }

            lock (_backlist)
            {
                _backlist.AddLast(action);
            }
        }

        public static void Clear()
        {
            lock (_backlist)
            {
                _backlist.Clear();
            }

            if (_mb != null)
            {
                _mb.StopAllCoroutines();
            }
        }

        void OnDestroy()
        {
            _mb = null;
            ResourceManager.Close();
        }
    }
}
