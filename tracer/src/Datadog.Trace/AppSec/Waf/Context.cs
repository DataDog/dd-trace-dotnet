// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();
        private readonly IDictionary<string, object> _addresses = new Dictionary<string, object>();
        private readonly IntPtr contextHandle;
        private readonly WafNative wafNative;
        private readonly Encoder encoder;
        private readonly List<Obj> argCache = new();
        private readonly Stopwatch _stopwatch;
        private bool disposed = false;
        private ulong _totalRuntimeOverRuns;

        public Context(IntPtr contextHandle, WafNative wafNative, Encoder encoder)
        {
            this.contextHandle = contextHandle;
            this.wafNative = wafNative;
            this.encoder = encoder;
            _stopwatch = new Stopwatch();
        }

        ~Context() => Dispose(false);

        public IResult Run(ulong timeoutMicroSeconds)
        {
            var pwArgs = encoder.Encode(_addresses, argCache, applySafetyLimits: true);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var parameters = Encoder.FormatArgs(_addresses);
                Log.Debug("DDAS-0010-00: Executing AppSec In-App WAF with parameters: {Parameters}", parameters);
            }

            var rawArgs = pwArgs.RawPtr;
            DdwafResultStruct retNative = default;
            var code = wafNative.Run(contextHandle, rawArgs, ref retNative, timeoutMicroSeconds);
            _stopwatch.Stop();
            _totalRuntimeOverRuns += retNative.TotalRuntime / 1000;
            var result = new Result(retNative, code, wafNative, _totalRuntimeOverRuns, (ulong)_stopwatch.Elapsed.TotalMilliseconds * 1000);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "DDAS-0011-00: AppSec In-App WAF returned: {ReturnCode} {Data}",
                    result.ReturnCode,
                    result.Data);
            }

            return result;
        }

        public void AggregateAddresses(IDictionary<string, object> args)
        {
            foreach (var item in args)
            {
                _addresses[item.Key] = item.Value;
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

            wafNative.ContextDestroy(contextHandle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
