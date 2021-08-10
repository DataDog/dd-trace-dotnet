// <copyright file="Rule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Rule : IDisposable
    {
        private IntPtr ruleHandle;
        private bool disposed;

        public Rule(IntPtr ruleHandle)
        {
            this.ruleHandle = ruleHandle;
        }

        ~Rule()
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
            Native.pw_clearRuleH(ruleHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
