// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();
        private readonly IntPtr contextHandle;
        private bool disposed = false;

        public Context(IntPtr contextHandle)
        {
            this.contextHandle = contextHandle;
        }

        ~Context()
        {
            Dispose(false);
        }

        public IResult Run(IDictionary<string, object> args)
        {
            // do not add a using or call dispose in some other way here
            // when passing args to Native.Run it will take ownership and free them
            var pwArgs = Encoder.Encode(args);

            var rawAgs = pwArgs.RawPtr;
            DdwafResultStruct retNative = default;

            var code = WafNative.Run(contextHandle, rawAgs, ref retNative, 1000000);

            var ret = new Result(retNative, code);

            return ret;
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            WafNative.ContextDestroy(contextHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
