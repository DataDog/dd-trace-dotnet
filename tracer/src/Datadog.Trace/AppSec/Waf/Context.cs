// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();
        private readonly IntPtr contextHandle;
        private readonly WafNative wafNative;
        private readonly Encoder encoder;
        private readonly List<Obj> argCache = new();
        private bool disposed = false;

        public Context(IntPtr contextHandle, WafNative wafNative, Encoder encoder)
        {
            this.contextHandle = contextHandle;
            this.wafNative = wafNative;
            this.encoder = encoder;
        }

        ~Context()
        {
            Dispose(false);
        }

        public IResult Run(IDictionary<string, object> args)
        {
            var pwArgs = encoder.Encode(args, argCache);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var parameters = Encoder.FormatArgs(args);
                Log.Debug("DDAS-0010-00: Executing AppSec In-App WAF with parameters: {Parameters}", parameters);
            }

            var rawAgs = pwArgs.RawPtr;
            DdwafResultStruct retNative = default;

            var code = wafNative.Run(contextHandle, rawAgs, ref retNative, 1000000);

            var ret = new Result(retNative, code, wafNative);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug<ReturnCode, string>(
                    "DDAS-0011-00: AppSec In-App WAF returned: {ReturnCode} {Data} Took {PerfTotalRuntime} ms",
                    ret.ReturnCode,
                    ret.Data,
                    retNative.PerfTotalRuntime);
            }

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

            wafNative.ContextDestroy(contextHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
