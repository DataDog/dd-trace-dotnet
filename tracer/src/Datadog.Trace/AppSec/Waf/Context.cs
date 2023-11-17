// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();

        // the context handle should be locked, it is not safe for concurrent access and two
        // waf events may be processed at the same time due to code being run asynchronously
        private readonly IntPtr _contextHandle;

        private readonly Waf _waf;

        private readonly List<IntPtr> _argCache;
        private readonly Stopwatch _stopwatch;
        private readonly WafLibraryInvoker _wafLibraryInvoker;

        private bool _disposed;
        private ulong _totalRuntimeOverRuns;

        // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held
        private Context(IntPtr contextHandle, Waf waf, WafLibraryInvoker wafLibraryInvoker)
        {
            _contextHandle = contextHandle;
            _waf = waf;
            _wafLibraryInvoker = wafLibraryInvoker;
            _stopwatch = new Stopwatch();
            _argCache = new(64);
        }

        ~Context() => Dispose(false);

        public static IContext? GetContext(IntPtr contextHandle, Waf waf, WafLibraryInvoker wafLibraryInvoker)
        {
            // in high concurrency, the waf passed as argument here could have been disposed just above in between creation / waf update so last test here
            if (waf.Disposed)
            {
                wafLibraryInvoker.ContextDestroy(contextHandle);
                return null;
            }

            return new Context(contextHandle, waf, wafLibraryInvoker);
        }

        public IResult? Run(IDictionary<string, object> addresses, ulong timeoutMicroSeconds)
        {
            if (_disposed)
            {
                ThrowHelper.ThrowException("Can't run WAF when context is disposed");
            }

            DdwafResultStruct retNative = default;

            if (_waf.Disposed)
            {
                Log.Warning("Context can't run when waf handle has been disposed. This shouldn't have happened with the locks, check concurrency.");
                return null;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var parameters = Encoder.FormatArgs(addresses);
                Log.Debug("DDAS-0010-00: Executing AppSec In-App WAF with parameters: {Parameters}", parameters);
            }

            // not restart cause it's the total runtime over runs, and we run several * during request
            _stopwatch.Start();

            WafReturnCode code;
            lock (_stopwatch)
            {
                var pool = Encoder.Pool;
                try
                {
                    var pwArgs = Encoder.Encode(addresses, applySafetyLimits: true, argToFree: _argCache, pool: pool);
                    var empty = Encoder.Encode(new Dictionary<string, object>(), applySafetyLimits: true, argToFree: _argCache, pool: pool);
                    code = _waf.Run(_contextHandle, ref pwArgs, ref empty, ref retNative, timeoutMicroSeconds);
                }
                finally
                {
                    pool.Return(_argCache);
                    _argCache.Clear();
                }
            }

            _stopwatch.Stop();
            _totalRuntimeOverRuns += retNative.TotalRuntime / 1000;
            var result = new Result(retNative, code, _totalRuntimeOverRuns, (ulong)(_stopwatch.Elapsed.TotalMilliseconds * 1000));
            _wafLibraryInvoker.ResultFree(ref retNative);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "DDAS-0011-00: AppSec In-App WAF returned: {ReturnCode} {Data}",
                    result.ReturnCode,
                    result.Data);
            }

            return result;
        }

        public void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (_stopwatch)
            {
                _wafLibraryInvoker.ContextDestroy(_contextHandle);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
