using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace UnityFS.Utils
{
    using UnityEngine;

    public class CountdownObject
    {
        private int _countdown = 1;
        private Action _callback;

        public CountdownObject(Action callback)
        {
            _callback = callback;
        }

        public void Add()
        {
            if (_countdown == 0)
            {
                throw new InvalidOperationException();
            }
            ++_countdown;
        }

        public void Remove()
        {
            if (_countdown == 0)
            {
                throw new InvalidOperationException();
            }
            --_countdown;
            if (_countdown == 0)
            {
                _callback?.Invoke();
            }
        }

        public void Start()
        {
            Remove();
        }
    }
}