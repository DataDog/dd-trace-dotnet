// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();
        private readonly IntPtr contextHandle;
        private readonly List<Obj> argCache = new List<Obj>();
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
            var pwArgs = Encoder.Encode(args, argCache);

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

            foreach (var arg in argCache)
            {
                arg.Dispose();
            }

            WafNative.ContextDestroy(contextHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
