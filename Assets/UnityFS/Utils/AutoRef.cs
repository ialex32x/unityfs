using System;
using System.IO;
using System.Collections.Generic;

namespace UnityFS
{
    using UnityEngine;

    public class AutoRef
    {
        private IRefCounted _obj;

        public AutoRef(IRefCounted obj)
        {
            _obj = obj;
            _obj.AddRef();
        }

        ~AutoRef()
        {
            JobScheduler.DispatchMain(() => 
            {
                _obj.RemoveRef();
            });
        }
    }
}
