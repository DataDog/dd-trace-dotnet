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
    private readonly object _sync = new();
    private readonly IWafLibraryInvoker _wafLibraryInvoker;
    private readonly IEncoder _encoder;
    private readonly UserEventsState _userEventsState = new();

    // subcontexts kept open across batches of one operation, guarded by the same lock as the context
    private Dictionary<ulong, OpenSubcontext>? _openSubcontexts;

    private bool _disposed;

    // per-request totals, tracked separately for WAF and RASP runs
    private WafRunTotals _wafTotals;
    private WafRunTotals _raspTotals;

    // Beware this class is created on a thread but can be disposed on another so don't trust the lock is not going to be held
    private Context(IntPtr contextHandle, IWaf waf, IWafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
    {
        _contextHandle = contextHandle;
        _waf = waf;
        _wafLibraryInvoker = wafLibraryInvoker;
        _encoder = encoder;
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
        => RunInternal(addressData, false, timeoutMicroSeconds, isRasp: false, out _);

    public IResult? RunWithEphemeral(IDictionary<string, object> ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp, out WafOutcome outcome)
        => RunInternal(ephemeralAddressData, true, timeoutMicroSeconds, isRasp, out outcome);

    public IResult? RunInSubcontext(ulong subcontextId, IDictionary<string, object> addressData, bool closeSubcontext, ulong timeoutMicroSeconds, bool isRasp, out WafOutcome outcome)
        => RunInternal(addressData, true, timeoutMicroSeconds, isRasp, out outcome, subcontextId, closeSubcontext);

    public void CloseSubcontext(ulong subcontextId)
    {
        lock (_sync)
        {
            if (_disposed || _openSubcontexts is null || !_openSubcontexts.TryGetValue(subcontextId, out var subcontext))
            {
                return;
            }

            _openSubcontexts.Remove(subcontextId);
            subcontext.Destroy(_wafLibraryInvoker);
        }
    }

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

    // a RASP run reports its binding errors as rasp.error instead, which its caller does from the
    // BindingFailed outcome the run returns
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
    /// <param name="outcome">why the run produced no result</param>
    /// <param name="subcontextId">when set, the ephemeral batch runs in the subcontext kept for this id</param>
    /// <param name="closeSubcontext">whether the kept subcontext is released after this batch</param>
    private unsafe Result? RunInternal(IDictionary<string, object>? addressData, bool ephemeral, ulong timeoutMicroSeconds, bool isRasp, out WafOutcome outcome, ulong? subcontextId = null, bool closeSubcontext = true)
    {
        DdwafObjectStruct retNative = default;

        if (_waf.Disposed)
        {
            Log.Warning("Context can't run when waf handle has been disposed. This shouldn't have happened with the locks, check concurrency.");
            outcome = WafOutcome.WafUnavailable;
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

        Result result;
        lock (_sync)
        {
            if (_disposed)
            {
                // the context is disposed when the request it belongs to ends, so a run that gets here
                // arrived too late rather than failed
                Log.Information("Can't run WAF when context is disposed");
                outcome = WafOutcome.RequestEnded;
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
                outcome = WafOutcome.BindingFailed;
                return null;
            }

            // started inside the lock so that a run waiting for a concurrent one doesn't count its time
            var start = Stopwatch.GetTimestamp();
            var args = _encoder.Encode(addressData!, applySafetyLimits: true);
            var truncated = args.Truncated;
            WafReturnCode code;

            // WARNING: Don't use ref here, we need to make a copy because args is on the heap
            var argsValue = args.ResultDdwafObject;

            if (ephemeral)
            {
                // One subcontext per ephemeral batch, or per operation when an id is given. Its evaluation
                // caches are what makes a rule report its match only once, so a subcontext shared by every
                // RASP call of a request would silently swallow all matches but the first one.
                OpenSubcontext? subcontext = null;
                if (subcontextId is { } id)
                {
                    _openSubcontexts?.TryGetValue(id, out subcontext);
                }

                if (subcontext is null)
                {
                    var subcontextHandle = _waf.SubcontextInit(_contextHandle);
                    if (subcontextHandle == IntPtr.Zero)
                    {
                        Log.Error("WAF ddwaf_subcontext_init failed, the ephemeral run was skipped");
                        RecordBindingError(isRasp);
                        args.Dispose();
                        outcome = WafOutcome.BindingFailed;
                        return null;
                    }

                    subcontext = new OpenSubcontext(subcontextHandle);
                    if (subcontextId is { } newId && !closeSubcontext)
                    {
                        (_openSubcontexts ??= new()).Add(newId, subcontext);
                    }
                }

                // the subcontext reads this batch until it is destroyed, so it owns the input buffers
                subcontext.Inputs.Add(args);

                try
                {
                    code = _waf.SubcontextEval(subcontext.Handle, &argsValue, ref retNative, timeoutMicroSeconds);
                }
                finally
                {
                    // once the subcontext is gone its input buffers can be released instead of piling up
                    // for the whole request
                    if (subcontextId is null || closeSubcontext)
                    {
                        if (subcontextId is { } closedId)
                        {
                            _openSubcontexts?.Remove(closedId);
                        }

                        subcontext.Destroy(_wafLibraryInvoker);
                    }
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

            ref var totals = ref isRasp ? ref _raspTotals : ref _wafTotals;
            totals.AddDurationWithBindings(Stopwatch.GetTimestamp() - start);
            result = new Result(ref retNative, code, ref totals, isRasp, truncated);
        }

        outcome = WafOutcome.Success;

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
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // subcontexts left open by operations that never sent their last batch go before their parent
            if (_openSubcontexts is not null)
            {
                foreach (var subcontext in _openSubcontexts.Values)
                {
                    subcontext.Destroy(_wafLibraryInvoker);
                }

                _openSubcontexts = null;
            }

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

    private sealed class OpenSubcontext(IntPtr handle)
    {
        public IntPtr Handle { get; } = handle;

        public List<IEncodeResult> Inputs { get; } = new(2);

        public void Destroy(IWafLibraryInvoker wafLibraryInvoker)
        {
            // the subcontext reads its inputs, so it goes first
            wafLibraryInvoker.SubcontextDestroy(Handle);
            foreach (var input in Inputs)
            {
                input.Dispose();
            }

            Inputs.Clear();
        }
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
