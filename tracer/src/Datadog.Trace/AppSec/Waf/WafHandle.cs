// <copyright file="WafHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf
{
    internal class WafHandle : IDisposable
    {
        private readonly WafNative _wafNative;
        private IntPtr ruleHandle;
        private bool disposed;

        public WafHandle(WafNative wafNative, IntPtr ruleHandle)
        {
            _wafNative = wafNative;
            this.ruleHandle = ruleHandle;
        }

        ~WafHandle()
        {
            Dispose(false);
        }

        public IntPtr Handle
        {
            get { return ruleHandle; }
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            _wafNative.Destroy(ruleHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
