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
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf;

/// <summary>
/// Context type using the underlying native library here: https://github.com/DataDog/libddwaf/blob/7a17b8d31b491e329f10eae20b07a619910aa888/src/context.hpp#L147
/// </summary>
internal sealed class Context : IContext
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();

    // the context handle should be locked, it is not safe for concurrent access and two
    // waf events may be processed at the same time due to code being run asynchronously
    private readonly IntPtr _contextHandle;

    private readonly IWaf _waf;

    private readonly List<IEncodeResult> _encodeResults;
    private readonly Stopwatch _stopwatch;
    private readonly IWafLibraryInvoker _wafLibraryInvoker;
    private readonly IEncoder _encoder;
    private readonly UserEventsState _userEventsState = new();

    private bool _disposed;
    private ulong _totalRuntimeOverRuns;

    // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held
    private Context(IntPtr contextHandle, IWaf waf, IWafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
    {
        _contextHandle = contextHandle;
        _waf = waf;
        _wafLibraryInvoker = wafLibraryInvoker;
        _encoder = encoder;
        _stopwatch = new Stopwatch();
        _encodeResults = new(64);
    }

    ~Context() => Dispose(false);

    public static IContext? GetContext(IntPtr contextHandle, IWaf waf, IWafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
    {
        // in high concurrency, the waf passed as argument here could have been disposed just above in between creation / waf update so last test here
        if (waf.Disposed)
        {
            wafLibraryInvoker.ContextDestroy(contextHandle);
            return null;
        }

        return new Context(contextHandle, waf, wafLibraryInvoker, encoder);
    }

    public IResult? Run(IDictionary<string, object> addressData, ulong timeoutMicroSeconds)
        => RunInternal(addressData, false, timeoutMicroSeconds);

    public IResult? RunWithEphemeral(IDictionary<string, object> ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp)
        => RunInternal(ephemeralAddressData, true, timeoutMicroSeconds, isRasp);

    public Dictionary<string, object> FilterAddresses(IDatadogSecurity security, string? userId = null, string? userLogin = null, string? userSessionId = null, bool fromSdk = false)
    {
        var addresses = new Dictionary<string, object>();
        if (ShouldRunWith(security, _userEventsState.Id, userId, AddressesConstants.UserId, fromSdk))
        {
            addresses[AddressesConstants.UserId] = userId!;
        }

        if (ShouldRunWith(security, _userEventsState.Login, userLogin, AddressesConstants.UserLogin, fromSdk))
        {
            addresses[AddressesConstants.UserLogin] = userLogin!;
        }

        if (ShouldRunWith(security, _userEventsState.SessionId, userSessionId, AddressesConstants.UserSessionId, fromSdk))
        {
            addresses[AddressesConstants.UserSessionId] = userSessionId!;
        }

        return addresses;
    }

    public bool ShouldRunWithSession(IDatadogSecurity security, string? userSessionId = null, bool fromSdk = false) => ShouldRunWith(security, _userEventsState.SessionId, userSessionId, AddressesConstants.UserSessionId, fromSdk);

    public void CommitUserRuns(Dictionary<string, object> addresses, bool fromSdk)
    {
        if (addresses.TryGetValue(AddressesConstants.UserId, out var address))
        {
            _userEventsState.Id = new(address.ToString(), fromSdk);
        }

        if (addresses.TryGetValue(AddressesConstants.UserLogin, out address))
        {
            _userEventsState.Login = new(address.ToString(), fromSdk);
        }

        if (addresses.TryGetValue(AddressesConstants.UserSessionId, out address))
        {
            _userEventsState.SessionId = new(address.ToString(), fromSdk);
        }
    }

    private static void RecordBindingError(bool isRasp)
    {
        if (!isRasp)
        {
            TelemetryFactory.Metrics.RecordCountWafError(MetricTags.WafError.BindingError);
        }
    }

    private bool ShouldRunWith(IDatadogSecurity security, UserEventsState.UserRecord? previousUserRecord, string? value, string address, bool fromSdk)
    {
        if (value is null || !security.AddressEnabled(address))
        {
            return false;
        }

        if (!previousUserRecord.HasValue)
        {
            return true;
        }

        var previousValue = previousUserRecord.Value.Value;
        var previousValueFromSdk = previousUserRecord.Value.FromSdk;
        var differentValue = string.Compare(previousValue, value, StringComparison.OrdinalIgnoreCase) is not 0;
        return differentValue && (fromSdk || !previousValueFromSdk);
    }

    /// <summary>
    /// Runs the WAF over one batch of addresses.
    /// </summary>
    /// <param name="addressData">the addresses to evaluate</param>
    /// <param name="ephemeral">when true the batch is evaluated in its own subcontext, so that its side
    /// effects don't outlive the call; this is what RASP relies on</param>
    /// <param name="timeoutMicroSeconds">the WAF budget for this run</param>
    /// <param name="isRasp">whether this run should be reported as a RASP run</param>
    private unsafe Result? RunInternal(IDictionary<string, object>? addressData, bool ephemeral, ulong timeoutMicroSeconds, bool isRasp = false)
    {
        DdwafObjectStruct retNative = default;

        if (_waf.Disposed)
        {
            Log.Warning("Context can't run when waf handle has been disposed. This shouldn't have happened with the locks, check concurrency.");
            return null;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var parameters = addressData == null ? string.Empty : Encoder.FormatArgs(addressData);
            Log.Debug(
                "DDAS-0010-00: Executing AppSec In-App WAF with {Kind} parameters: {Parameters}",
                ephemeral ? "ephemeral" : "persistent",
                parameters);
        }

        // not restart because it's the total runtime over runs, and we run several * during request
        _stopwatch.Start();
        WafReturnCode code;
        bool truncated;
        lock (_stopwatch)
        {
            if (_disposed)
            {
                Log.Information("Can't run WAF when context is disposed");
                return null;
            }

            // NOTE: the WAF must be called with a valid map. Calling _encoder.Encode(null) results in an
            // invalid object that will cause the WAF to error, and it doesn't make sense to propagate that
            // error. The WAF can be called with an empty dictionary (though we should avoid doing this),
            // but an empty ephemeral batch is pointless so it is rejected like it was before subcontexts.
            if (ephemeral ? addressData is not { Count: > 0 } : addressData is null)
            {
                Log.Error("The WAF was called without any address data");
                RecordBindingError(isRasp);
                return null;
            }

            var args = _encoder.Encode(addressData!, applySafetyLimits: true);
            truncated = args.Truncated;

            // WARNING: Don't use ref here, we need to make a copy because args is on the heap
            var argsValue = args.ResultDdwafObject;

            if (ephemeral)
            {
                // One subcontext per ephemeral batch. Its evaluation caches are what makes a rule report
                // its match only once, so a subcontext shared by every RASP call of a request would
                // silently swallow all matches but the first one.
                var subcontextHandle = _waf.SubcontextInit(_contextHandle);
                if (subcontextHandle == IntPtr.Zero)
                {
                    Log.Error("WAF ddwaf_subcontext_init failed, the ephemeral run was skipped");
                    RecordBindingError(isRasp);
                    args.Dispose();

                    // nothing ran, so don't let this call's wall clock leak into the aggregated runtime
                    _stopwatch.Stop();
                    return null;
                }

                try
                {
                    code = _waf.SubcontextEval(subcontextHandle, &argsValue, ref retNative, timeoutMicroSeconds);
                }
                finally
                {
                    // the subcontext is the only reader of this batch, so once it is gone the input
                    // buffers can be released instead of piling up for the whole request
                    _wafLibraryInvoker.SubcontextDestroy(subcontextHandle);
                    args.Dispose();
                }
            }
            else
            {
                // WARNING: DO NOT DISPOSE the encoded arguments until the end of this class's lifecycle,
                // i.e. in Dispose. libddwaf is given a null allocator on evaluation, so it never copies
                // nor frees the input: those buffers have to outlive the context that reads them,
                // otherwise the waf might crash with a fatal exception. They don't need to be pinned, as
                // behind the scenes they are heap allocated pointers (through waf helpers via the legacy
                // encoder or manually HC allocs via the new encoder).
                _encodeResults.Add(args);
                code = _waf.ContextEval(_contextHandle, &argsValue, ref retNative, timeoutMicroSeconds);
            }
        }

        _stopwatch.Stop();
        var result = new Result(ref retNative, code, ref _totalRuntimeOverRuns, (ulong)(_stopwatch.Elapsed.TotalMilliseconds * 1000), isRasp, truncated);

        // the result was allocated by the WAF with the output allocator given to ddwaf_context_init, which is the default one
        _wafLibraryInvoker.ObjectDestroy(ref retNative);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "DDAS-0011-00: AppSec In-App WAF returned: {ReturnCode} {BlockInfo} {Data}",
                result.ReturnCode,
                result.BlockInfo,
                result.Data);
        }

        return result;
    }

    public void Dispose(bool disposing)
    {
        lock (_stopwatch)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // destroy the consumer of our input buffers first: the context reads the memory owned by
            // _encodeResults, which is why that one is released last
            _wafLibraryInvoker.ContextDestroy(_contextHandle);

            // WARNING do not move this above, this should only be disposed in the end of the context's life
            foreach (var encodeResult in _encodeResults)
            {
                encodeResult.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal sealed record UserEventsState
    {
        /// <summary>
        /// Gets or sets a string for the value and bool for if it came from sdk
        /// </summary>
        internal UserRecord? Id { get; set; }

        /// <summary>
        /// Gets or sets a string for the value and bool for if it came from sdk
        /// </summary>
        internal UserRecord? Login { get; set; }

        /// <summary>
        /// Gets or sets a string for the value and bool for if it came from sdk
        /// </summary>
        internal UserRecord? SessionId { get; set; }

        internal record struct UserRecord(string? Value, bool FromSdk);
    }
}
