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
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf;

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
    private ulong _totalRuntimeOverRuns = 0;

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
        => RunInternal(addressData, null, timeoutMicroSeconds);

    public IResult? RunWithEphemeral(IDictionary<string, object> ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp)
        => RunInternal(null, ephemeralAddressData, timeoutMicroSeconds, isRasp);

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

    private unsafe IResult? RunInternal(IDictionary<string, object>? persistentAddressData, IDictionary<string, object>? ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp = false)
    {
        DdwafObjectStruct retNative = default;

        if (_waf.Disposed)
        {
            Log.Warning("Context can't run when waf handle has been disposed. This shouldn't have happened with the locks, check concurrency.");
            return null;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var persistentParameters = persistentAddressData == null ? string.Empty : Encoder.FormatArgs(persistentAddressData);
            var ephemeralParameters = ephemeralAddressData == null ? string.Empty : Encoder.FormatArgs(ephemeralAddressData);
            Log.Debug(
                "DDAS-0010-00: Executing AppSec In-App WAF with parameters: persistent: {PersistentParameters}, ephemeral: {EphemeralParameters}",
                persistentParameters,
                ephemeralParameters);
        }

        // not restart because it's the total runtime over runs, and we run several * during request
        _stopwatch.Start();
        WafReturnCode code;
        lock (_stopwatch)
        {
            if (_disposed)
            {
                Log.Information("Can't run WAF when context is disposed");
                return null;
            }

            // NOTE: the WAF must be called with either pwPersistentArgs or pwEphemeralArgs (or both) pointing to
            // a valid structure. Failure to do so, results in a WAF error. It doesn't makes sense to propagate this
            // error.
            // Calling _encoder.Encode(null) results in a null object that will cause the WAF to error
            // The WAF can be called with an empty dictionary (though we should avoid doing this).

            DdwafObjectStruct pwPersistentArgs = default;
            DdwafObjectStruct pwEphemeralArgsValue = default;

            if (persistentAddressData is not null)
            {
                var persistentArgs = _encoder.Encode(persistentAddressData, applySafetyLimits: true);
                pwPersistentArgs = persistentArgs.ResultDdwafObject;
                _encodeResults.Add(persistentArgs);
            }

            // pwEphemeralArgs follow a different lifecycle and should be disposed immediately
            using var ephemeralArgs = ephemeralAddressData is { Count: > 0 }
                                          ? _encoder.Encode(ephemeralAddressData, applySafetyLimits: true)
                                          : null;

            if (persistentAddressData is null && ephemeralArgs is null)
            {
                Log.Error("Both pwPersistentArgs and pwEphemeralArgs are null");
                return null;
            }

            if (ephemeralArgs is not null)
            {
                // WARNING: Don't use ref here, we need to make a copy because ephemeralArgs is on the heap
                pwEphemeralArgsValue = ephemeralArgs.ResultDdwafObject;
            }

            // WARNING: DO NOT DISPOSE pwPersistentArgs until the end of this class's lifecycle, i.e in the dispose. Otherwise waf might crash with fatal exception.
            code = _waf.Run(_contextHandle, persistentAddressData != null ? &pwPersistentArgs : null, ephemeralArgs != null ? &pwEphemeralArgsValue : null, ref retNative, timeoutMicroSeconds);
        }

        _stopwatch.Stop();
        var result = new Result(ref retNative, code, ref _totalRuntimeOverRuns, (ulong)(_stopwatch.Elapsed.TotalMilliseconds * 1000), isRasp);
        _wafLibraryInvoker.ObjectFree(ref retNative);

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

            // WARNING do not move this above, this should only be disposed in the end of the context's life
            foreach (var encodeResult in _encodeResults)
            {
                encodeResult.Dispose();
            }

            _wafLibraryInvoker.ContextDestroy(_contextHandle);
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
