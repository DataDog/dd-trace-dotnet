// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        private readonly object _sync = new object();
        private readonly IntPtr _contextHandle;

        private readonly Waf _waf;

        private readonly List<Obj> _argCache = new();
        private readonly List<GCHandle> _argCache2 = new();
        private readonly List<IntPtr> _argCache3 = new();
        private readonly Stopwatch _stopwatch;
        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly List<IntPtr> _utf8StringsList = new();

        private bool _disposed;
        private ulong _totalRuntimeOverRuns;

        // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held
        private Context(IntPtr contextHandle, Waf waf, WafLibraryInvoker wafLibraryInvoker)
        {
            _contextHandle = contextHandle;
            _waf = waf;
            _wafLibraryInvoker = wafLibraryInvoker;
            _utf8StringsList = new List<IntPtr>();
            _stopwatch = new Stopwatch();
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

        public IResult? Run3(IDictionary<string, object> addresses, ulong timeoutMicroSeconds)
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
            var pwArgs = Encoder.Encode3(addresses, _wafLibraryInvoker, gcHandles: _argCache2, applySafetyLimits: true);

            DDWAF_RET_CODE code;
            lock (_sync)
            {
                code = _waf.Run(_contextHandle, ref pwArgs, ref retNative, timeoutMicroSeconds);
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

        public IResult? Run2(IDictionary<string, object> addresses, ulong timeoutMicroSeconds)
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

            DDWAF_RET_CODE code;
            lock (_sync)
            {
                var pwArgs = Encoder.Encode2(addresses, applySafetyLimits: true, argToFree: _argCache2, argToFree2: _utf8StringsList);
                code = WafRun(timeoutMicroSeconds, ref pwArgs, ref retNative);
#if NETCOREAPP3_1_OR_GREATER
                foreach (var nMem in _utf8StringsList)
                {
                    Encoder.Pool.Return(nMem);
                }
#endif
                _utf8StringsList.Clear();
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private DDWAF_RET_CODE WafRun(ulong timeoutMicroSeconds, ref DdwafObjectStruct pwArgs, ref DdwafResultStruct retNative)
        {
            return _waf.Run(_contextHandle, ref pwArgs, ref retNative, timeoutMicroSeconds);
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
            using var pwArgs = Encoder.Encode(addresses, _wafLibraryInvoker, _argCache, applySafetyLimits: true);
            var rawArgs = pwArgs.RawPtr;

            DDWAF_RET_CODE code;
            lock (_sync)
            {
                code = _waf.Run(_contextHandle, rawArgs, ref retNative, timeoutMicroSeconds);
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

            foreach (var arg in _argCache)
            {
                arg.Dispose();
            }

            lock (_sync)
            {
                _wafLibraryInvoker.ContextDestroy(_contextHandle);
            }

            foreach (var arg in _argCache2)
            {
                arg.Free();
            }

            foreach (var arg in _argCache3)
            {
                Marshal.FreeHGlobal(arg);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
