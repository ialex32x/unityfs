using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public abstract class AbstractFileSystem : IFileSystem
    {
        protected bool _loaded;
        private List<Action> _callbacks = new List<Action>();

        public event Action completed
        {
            add
            {
                if (_loaded)
                {
                    value();
                }
                else
                {
                    _callbacks.Add(value);
                }
            }

            remove
            {
                _callbacks.Remove(value);
            }
        }

        public bool isLoaded
        {
            get { return _loaded; }
        }

        protected void Complete()
        {
            if (!_loaded)
            {
                _loaded = true;
                OnLoaded();
            }
        }

        protected void OnLoaded()
        {
            while (_callbacks.Count > 0)
            {
                var callback = _callbacks[0];
                _callbacks.RemoveAt(0);
                callback();
            }
        }

        public abstract bool Exists(string filename);

        public abstract byte[] ReadAllBytes(string filename);
    }
}
