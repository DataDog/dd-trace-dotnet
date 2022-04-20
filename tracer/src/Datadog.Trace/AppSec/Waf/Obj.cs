// <copyright file="Obj.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    // NOTE: this is referred to as ddwaf_object in the C++ code, we call it Obj to avoid a naming clash
    internal class Obj : IDisposable
    {
        private readonly IntPtr ptr;
        private DdwafObjectStruct innerObj;
        private bool innerObjInitialized = false;
        private bool disposed = false;

        public Obj(IntPtr ptr) => this.ptr = ptr;

        ~Obj()
        {
            Dispose(false);
        }

        public ObjType ArgsType
        {
            get
            {
                Initialize();
                return Encoder.DecodeArgsType(innerObj.Type);
            }
        }

        public IntPtr RawPtr => ptr;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Marshal.FreeHGlobal(ptr);
        }

        private void Initialize()
        {
            if (innerObjInitialized)
            {
                return;
            }

            innerObjInitialized = true;
            innerObj = (DdwafObjectStruct)Marshal.PtrToStructure(ptr, typeof(DdwafObjectStruct));
        }
    }
}
