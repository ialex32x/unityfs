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
        private static Utils.RingBuffer<Action> _actions = new Utils.RingBuffer<Action>(20);
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
        }

        private void Update()
        {
            var action = _actions.Dequeue();
            while (action != null)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("JobScheduler.Update: {0}", exception);
                }
                action = _actions.Dequeue();
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

            // 需保证同时只有一个线程执行 enqueue
            lock (_backlist)
            {
                if (_backlist.Count == 0)
                {
                    if (!_actions.Enqueue(action))
                    {
                        _backlist.AddLast(action);
                    }
                    return;
                }
                _backlist.AddLast(action);
                while (_backlist.Count != 0)
                {
                    var last = _backlist.First.Value;
                    if (!_actions.Enqueue(last))
                    {
                        return;
                    }
                    _backlist.RemoveFirst();
                }
            }
        }

        void OnDestroy()
        {
            _mb = null;
            ResourceManager.Close();
        }
    }
}
