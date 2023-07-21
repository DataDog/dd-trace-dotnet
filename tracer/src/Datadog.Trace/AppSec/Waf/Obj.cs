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
        private IntPtr ptr;
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

        public long IntValue
        {
            get
            {
                Initialize();
                return innerObj.IntValue;
            }
        }

        public ulong UintValue
        {
            get
            {
                Initialize();
                return innerObj.UintValue;
            }
        }

        public nint InnerPtr
        {
            get
            {
                Initialize();
                return innerObj.Array;
            }
        }

        public DdwafObjectStruct InnerStruct
        {
            get
            {
                Initialize();
                return innerObj;
            }
        }

        public IntPtr RawPtr => ptr;

        public void Dispose(WafLibraryInvoker waf)
        {
            if (waf != null)
            {
                var rawPtr = ptr;
                waf.ObjectFreePtr(ref rawPtr);
                Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                ptr = IntPtr.Zero;
                return;
            }

            disposed = true;

            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
                ptr = IntPtr.Zero;
            }
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
