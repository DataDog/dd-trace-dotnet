// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.AppSec.Concurrency;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();
        private readonly IntPtr _contextHandle;
        private readonly Waf _waf;
        private readonly ReaderWriterLock _wafLocker;
        private readonly List<Obj> _argCache = new();
        private readonly Stopwatch _stopwatch;

        // this lock is for locking the context handle and argCache objects, which aren't thread safe
        private bool _disposed;
        private ulong _totalRuntimeOverRuns;

        // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held 
        private Context(IntPtr contextHandle, Waf waf, ReaderWriterLock wafLocker, out bool locked)
        {
            _wafLocker = wafLocker;
            locked = _wafLocker.IsReadLockHeld || _wafLocker.EnterReadLock();
            _contextHandle = contextHandle;
            _waf = waf;
            _stopwatch = new Stopwatch();
        }

        ~Context() => Dispose(false);

        public static IContext? GetContext(IntPtr contextHandle, Waf waf, ReaderWriterLock wafLocker, out bool locked)
        {
            var context = new Context(contextHandle, waf, wafLocker, out locked);
            return locked ? context : null;
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
            using var pwArgs = Encoder.Encode(addresses, _argCache, applySafetyLimits: true);
            var rawArgs = pwArgs.RawPtr;
            var code = _waf.Run(_contextHandle, rawArgs, ref retNative, timeoutMicroSeconds);

            _stopwatch.Stop();
            _totalRuntimeOverRuns += retNative.TotalRuntime / 1000;
            var result = new Result(retNative, code, _totalRuntimeOverRuns, (ulong)(_stopwatch.Elapsed.TotalMilliseconds * 1000));
            WafLibraryInvoker.ResultFree(ref retNative);

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

            foreach (var arg in _argCache)
            {
                arg.Dispose();
            }

            WafLibraryInvoker.ContextDestroy(_contextHandle);

            _wafLocker.ExitReadLock();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
