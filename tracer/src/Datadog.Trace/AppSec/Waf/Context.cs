// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

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
            LogParametersIfDebugEnabled(args);

            var pwArgs = Encoder.Encode(args, argCache);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Executing AppSec In-App WAF");
            }

            var rawAgs = pwArgs.RawPtr;
            DdwafResultStruct retNative = default;

            var code = WafNative.Run(contextHandle, rawAgs, ref retNative, 1000000);

            var ret = new Result(retNative, code);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug<ReturnCode, string>(
                    @"AppSec In-App WAF returned: {ReturnCode} {Data}
Took {PerfTotalRuntime} ms",
                    ret.ReturnCode,
                    ret.Data,
                    retNative.PerfTotalRuntime);
            }

            return ret;
        }

        private static void LogParametersIfDebugEnabled(IDictionary<string, object> args)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

                foreach (var kvp in args)
                {
                    sb.Append(kvp.Key);
                    sb.Append(": ");
                    sb.AppendLine(kvp.Value.ToString());
                }

                Log.Debug("Executing AppSec In-App WAF with parameters: {Parameters}", sb.ToString());
            }
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
