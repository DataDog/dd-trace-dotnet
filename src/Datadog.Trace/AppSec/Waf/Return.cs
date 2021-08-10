// <copyright file="Return.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Return : IReturn
    {
        private PWRet returnHandle;
        private bool disposed;

        public Return(PWRet returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        ~Return()
        {
            Dispose(false);
        }

        public ReturnCode ReturnCode
        {
            get { return Encoder.DecodeReturnCode(returnHandle.Action); }
        }

        public string Data
        {
            get { return Marshal.PtrToStringAnsi(returnHandle.Data); }
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Native.pw_freeReturn(returnHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
