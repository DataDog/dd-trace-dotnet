// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
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
        private readonly List<Obj> _argCacheLegacy;
        private readonly Stopwatch _stopwatch;
        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly bool _useLegacyEncoder;

        private bool _disposed;
        private ulong _totalRuntimeOverRuns;

        // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held
        private Context(IntPtr contextHandle, Waf waf, WafLibraryInvoker wafLibraryInvoker, bool useLegacyEncoder)
        {
            _contextHandle = contextHandle;
            _waf = waf;
            _wafLibraryInvoker = wafLibraryInvoker;
            _useLegacyEncoder = useLegacyEncoder;
            _stopwatch = new Stopwatch();
            _argCache = new(64);
            _argCacheLegacy = new();
        }

        ~Context() => Dispose(false);

        public static IContext? GetContext(IntPtr contextHandle, Waf waf, WafLibraryInvoker wafLibraryInvoker, bool useLegacyEncoder)
        {
            // in high concurrency, the waf passed as argument here could have been disposed just above in between creation / waf update so last test here
            if (waf.Disposed)
            {
                wafLibraryInvoker.ContextDestroy(contextHandle);
                return null;
            }

            return new Context(contextHandle, waf, wafLibraryInvoker, useLegacyEncoder);
        }

        public unsafe IResult? Run(IDictionary<string, object> addresses, ulong timeoutMicroSeconds)
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
                IntPtr pwArgs;
                if (_useLegacyEncoder)
                {
                    var args = EncoderLegacy.Encode(addresses, applySafetyLimits: true, wafLibraryInvoker: _wafLibraryInvoker, argCache: _argCacheLegacy);
                    pwArgs = args.RawPtr;
                }
                else
                {
                    var pool = Encoder.Pool;
                    var args = Encoder.Encode(addresses, applySafetyLimits: true, argToFree: _argCache, pool: pool);
                    pwArgs = (IntPtr)(&args);
                }

                // WARNING: DO NOT DISPOSE pwArgs until the end of this class's lifecycle, i.e in the dispose. Otherwise waf might crash with fatal exception.
                code = _waf.Run(_contextHandle, pwArgs, ref retNative, timeoutMicroSeconds);
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

            // WARNING do not move this above, this should only be disposed in the end of the context's life
            if (_useLegacyEncoder)
            {
                foreach (var arg in _argCacheLegacy)
                {
                    arg.Dispose();
                }
            }
            else
            {
                Encoder.Pool.Return(_argCache);
                _argCache.Clear();
            }

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
