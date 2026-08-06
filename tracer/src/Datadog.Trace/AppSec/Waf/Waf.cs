// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.AppSec.Waf
{
    /// <summary>
    /// Type using the underlying native library here: https://github.com/DataDog/libddwaf
    /// </summary>
    internal sealed class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";
        private const int BuilderLockTimeoutInMs = 4000;
        private const int DisposeTimeoutInMs = 15000;

        /// <summary>
        /// How long the operations that finish an interrupted disposal wait for the locks. Kept short
        /// because some of them run on the request path: if the lock isn't free, the next one tries.
        /// </summary>
        private const int PendingReleaseTimeoutInMs = 1000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly Concurrency.ReaderWriterLock _wafLocker = new();
        private readonly IEncoder _encoder;

        /// <summary>
        /// Guards <see cref="_wafBuilderHandle"/>. The native builder isn't thread safe and, unlike the
        /// WAF instance, it is mutated by <see cref="Update"/> and freed by <see cref="Dispose"/>, so
        /// both have to hold this lock for as long as they touch it. It is deliberately separate from
        /// <see cref="_wafLocker"/>: building an instance can take a while and must not block context
        /// creation. Whoever takes both always takes this one first.
        /// </summary>
        private readonly object _builderLock = new();

        /// <summary>
        /// The builder that produced <see cref="_wafHandle"/>. It holds every configuration applied so
        /// far and outlives each built instance, because an RCM update rebuilds from it. This instance
        /// owns it and releases it on <see cref="Dispose"/>. Only ever read or written under
        /// <see cref="_builderLock"/>.
        /// </summary>
        private IntPtr _wafBuilderHandle;
        private IntPtr _wafHandle;
        private int _disposeRequested;
        private bool? _isKnowAddressesSuported;

        internal Waf(IntPtr wafBuilderHandle, IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafBuilderHandle = wafBuilderHandle;
            _wafHandle = wafHandle;
            _encoder = encoder;
        }

        /// <summary>
        /// Gets a value indicating whether disposal has been requested. It is set before the native
        /// handles are released, so every operation bails out from that point on, and it stays set even
        /// if a lock timeout prevented the release: the handles are then freed by whichever operation
        /// releases the lock next, or by a later <see cref="Dispose"/>.
        /// </summary>
        public bool Disposed => Volatile.Read(ref _disposeRequested) != 0;

        /// <summary>
        /// Gets a value indicating whether there is still something for <see cref="ReleaseNativeHandles"/>
        /// to free. Lets the retry paths skip the locks once everything has been released.
        /// </summary>
        internal bool HasNativeHandles => Volatile.Read(ref _wafHandle) != IntPtr.Zero || Volatile.Read(ref _wafBuilderHandle) != IntPtr.Zero;

        /// <summary>
        /// Gets or sets how long <see cref="Dispose"/> waits for each of the locks it needs. Only the
        /// tests change it, to exercise the timeout path without waiting out the real timeout.
        /// </summary>
        internal int DisposeLockTimeoutInMs { get; set; } = DisposeTimeoutInMs;

        public string Version => _wafLibraryInvoker.GetVersion();

        /// <summary>
        /// Create a new waf object configured with the ruleset file
        /// </summary>
        /// <param name="wafLibraryInvoker">to invoke native methods on the waf's native library</param>
        /// <param name="obfuscationParameterKeyRegex">the regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="obfuscationParameterValueRegex">the regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious, </param>
        /// <param name="configurationStatus">can be null. RemoteConfig rules json. Takes precedence over rulesFile </param>
        /// <param name="useUnsafeEncoder">use legacy encoder</param>
        /// <param name="wafDebugEnabled">if debug level logs should be enabled for the WAF</param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static InitResult Create(
            WafLibraryInvoker wafLibraryInvoker,
            string obfuscationParameterKeyRegex,
            string obfuscationParameterValueRegex,
            ConfigurationState configurationStatus,
            bool useUnsafeEncoder = false,
            bool wafDebugEnabled = false)
        {
            // set the log level and setup the logger
            wafLibraryInvoker.SetupLogging(wafDebugEnabled);
            IEncoder encoder = useUnsafeEncoder ? new Encoder() : new EncoderLegacy(wafLibraryInvoker);

            // starts out invalid so that destroying it is a no-op if the WAF never writes any diagnostics
            var diagnostics = default(DdwafObjectStruct);
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);
            try
            {
                // Configure owns whatever it allocates until it hands it back here, so an exception
                // inside it can't strand a handle: it frees them itself and reports the failure.
                var result = wafConfigurator.Configure(configurationStatus, encoder, obfuscationParameterKeyRegex, obfuscationParameterValueRegex, ref diagnostics, configurationStatus.RuleSetTitle);
                InitResult? initResult = null;
                try
                {
                    initResult = InitResult.From(ref result);
                    return initResult;
                }
                finally
                {
                    // Ownership of the native handles is transferred to the Waf instance. When no Waf
                    // came out of it, either because the build failed, the ruleset had no usable rules,
                    // or InitResult.From threw, nothing else will ever release them.
                    if (initResult?.Waf is null)
                    {
                        if (result.WafHandle != IntPtr.Zero)
                        {
                            wafLibraryInvoker.Destroy(result.WafHandle);
                        }

                        if (result.WafBuilderHandle != IntPtr.Zero)
                        {
                            wafLibraryInvoker.DestroyBuilder(result.WafBuilderHandle);
                        }
                    }
                }
            }
            finally
            {
                wafLibraryInvoker.ObjectDestroy(ref diagnostics);
            }
        }

        public UpdateResult Update(ConfigurationState configurationStatus)
        {
            if (Disposed)
            {
                // Early bail out with no lock
                return UpdateResult.FromFailed("Waf is already disposed and can't be updated");
            }

            // Hold the builder lock for the whole build: the builder isn't thread safe, and without it
            // Dispose could run ddwaf_builder_destroy while a ddwaf_builder_* call is still in flight.
            if (!Monitor.TryEnter(_builderLock, BuilderLockTimeoutInMs))
            {
                Log.Error<int>("Couldn't acquire lock to update waf in {Timeout} ms", BuilderLockTimeoutInMs);
                TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                return UpdateResult.FromFailed("Couldn't acquire lock to update waf");
            }

            // starts out invalid so that destroying it is a no-op if the WAF never writes any diagnostics
            var diagnostics = default(DdwafObjectStruct);
            var wafConfigurator = new WafConfigurator(_wafLibraryInvoker);
            try
            {
                // dispose may have won the race for the builder lock, in which case the builder is gone
                if (Disposed || _wafBuilderHandle == IntPtr.Zero)
                {
                    TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                    return UpdateResult.FromFailed("Waf is already disposed and can't be updated");
                }

                var updateResult = wafConfigurator.Update(_wafBuilderHandle, configurationStatus, _encoder, ref diagnostics, configurationStatus.RuleSetTitle);
                if (!updateResult.Success || updateResult.WafHandle == _wafHandle || updateResult.WafHandle == IntPtr.Zero)
                {
                    Log.Warning("A waf update came from remote configuration but final merged dictionary for waf is empty, no update will be performed.");
                }
                else
                {
                    // the instance was built outside the lock, so until it is installed below this method
                    // is its only owner: every path that doesn't install it has to destroy it
                    var newHandle = updateResult.WafHandle;
                    if (_wafLocker.EnterWriteLock())
                    {
                        if (!Disposed)
                        {
                            // update within the lock as iis can recycle and cause dispose to happen at the same time
                            var oldHandle = _wafHandle;
                            _wafHandle = newHandle;
                            _wafLocker.ExitWriteLock();
                            // Safe to destroy oldHandle here: ddwaf_context_init() copies the ruleset
                            // shared_ptr into each context, so contexts hold their own independent reference.
                            // ddwaf_destroy() only decrements the handle's refcount; existing contexts remain
                            // valid. The write lock above ensures no concurrent ddwaf_context_init call was
                            // reading oldHandle when it is destroyed.
                            // See: https://github.com/DataDog/libddwaf/blob/main/src/waf.hpp#L28-L30
                            _wafLibraryInvoker.Destroy(oldHandle);
                        }
                        else
                        {
                            _wafLocker.ExitWriteLock();
                            _wafLibraryInvoker.Destroy(newHandle);
                            TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                            return UpdateResult.FromFailed("Waf is already disposed and can't be updated");
                        }
                    }
                    else
                    {
                        _wafLibraryInvoker.Destroy(newHandle);
                        TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                        return UpdateResult.FromFailed("Couldn't acquire lock to update waf: the new instance couldn't be installed");
                    }
                }

                if (updateResult.Success)
                {
                    TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Success);
                }
                else
                {
                    TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                }

                return updateResult;
            }
            catch (Exception e)
            {
                TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                return UpdateResult.FromException(e);
            }
            finally
            {
                _wafLibraryInvoker.ObjectDestroy(ref diagnostics);
                Monitor.Exit(_builderLock);

                // A Dispose that ran while this update held the builder lock may have had to give up
                // on releasing the native handles. Now that the lock is free, finish the job for it.
                ReleasePendingHandles();
            }
        }

        public bool IsKnowAddressesSuported()
        {
            if (_isKnowAddressesSuported is null)
            {
                _isKnowAddressesSuported = _wafLibraryInvoker.IsKnowAddressesSuported();
            }

            return _isKnowAddressesSuported.Value;
        }

        public string[] GetKnownAddresses()
        {
            bool lockAcquired = false;
            try
            {
                // ddwaf_known_addresses is explicitly documented as not thread-safe:
                // https://github.com/DataDog/libddwaf/blob/7a17b8d31b491e329f10eae20b07a619910aa888/docs/c-api/api.md?plain=1#L144
                // Internally it lazily populates root_addresses via ruleset::get_root_addresses(),
                // which has no synchronization. Concurrent calls race on that lazy init and corrupt
                // the vector, causing an AccessViolationException in Marshal.PtrToStringAnsi.
                // A write lock ensures exclusive access, matching the original intent.
                if (_wafLocker.EnterWriteLock())
                {
                    lockAcquired = true;

                    if (_wafHandle == IntPtr.Zero)
                    {
                        // the instance has already been released by a dispose
                        return Array.Empty<string>();
                    }

                    var result = _wafLibraryInvoker.GetKnownAddresses(_wafHandle);
                    return result;
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while getting known addresses");
                return Array.Empty<string>();
            }
            finally
            {
                if (lockAcquired)
                {
                    _wafLocker.ExitWriteLock();

                    // same as in CreateContext: a dispose blocked by this lock left the release to us
                    ReleasePendingHandles();
                }
            }
        }

        /// <summary>
        /// Requires a non disposed waf handle
        /// </summary>
        /// <returns>Context object to perform matching using the provided WAF instance</returns>
        /// <exception cref="Exception">Exception</exception>
        public IContext? CreateContext()
        {
            if (Disposed)
            {
                Log.Warning("Context can't be created as waf instance has been disposed.");
                return null;
            }

            IntPtr contextHandle;
            if (_wafLocker.EnterReadLock())
            {
                // Re-check Disposed inside the read lock. Dispose() sets Disposed and calls
                // Destroy(_wafHandle) while holding the write lock, which is mutually exclusive
                // with this read lock. Without this check, a Dispose() could slip in between the
                // outer Disposed check and acquiring the read lock, leaving _wafHandle pointing at
                // freed memory: ddwaf_context_init dereferences the handle (handle->create_context),
                // so calling InitContext on a destroyed handle is a use-after-free.
                if (Disposed)
                {
                    _wafLocker.ExitReadLock();
                    ReleasePendingHandles();
                    Log.Warning("Context can't be created as waf instance has been disposed.");
                    return null;
                }

                contextHandle = _wafLibraryInvoker.InitContext(_wafHandle);
                _wafLocker.ExitReadLock();

                // A dispose that started while this read lock was held couldn't take the write lock, so
                // it left the instance behind for whoever was in its way: that's us. Do it here, before
                // the context is handed out, so Context.GetContext sees the disposal and drops it.
                ReleasePendingHandles();
            }
            else
            {
                Log.Warning("Context couldn't be created as we couldn't acquire a reader lock");
                return null;
            }

            if (contextHandle == IntPtr.Zero)
            {
                Log.Error(InitContextError);
                throw new Exception(InitContextError);
            }

            return Context.GetContext(contextHandle, this, _wafLibraryInvoker, _encoder);
        }

        // Doesn't require a non disposed waf handle, but as the WAF instance needs to be valid for the lifetime of the context, if waf is disposed, don't run (unpredictable)
        public unsafe WafReturnCode ContextEval(IntPtr contextHandle, DdwafObjectStruct* rawData, ref DdwafObjectStruct retNative, ulong timeoutMicroSeconds)
            => _wafLibraryInvoker.ContextEval(contextHandle, rawData, ref retNative, timeoutMicroSeconds);

        public IntPtr SubcontextInit(IntPtr contextHandle) => _wafLibraryInvoker.SubcontextInit(contextHandle);

        public unsafe WafReturnCode SubcontextEval(IntPtr subcontextHandle, DdwafObjectStruct* rawData, ref DdwafObjectStruct retNative, ulong timeoutMicroSeconds)
            => _wafLibraryInvoker.SubcontextEval(subcontextHandle, rawData, ref retNative, timeoutMicroSeconds);

        public void Dispose()
        {
            // Flag the disposal first so every other operation bails out, then release. Disposing twice
            // is not a no-op on purpose: if the first call couldn't take a lock it left a handle behind,
            // and this is one of the two places that retries (the other being Update, on its way out).
            Volatile.Write(ref _disposeRequested, 1);

            if (HasNativeHandles)
            {
                ReleaseNativeHandles(DisposeLockTimeoutInMs);
            }
        }

        /// <summary>
        /// Finishes a disposal that couldn't release everything because this operation was holding one of
        /// the locks it needed. Call it right after releasing a lock, never while still holding one: the
        /// operation that got in the way of the disposal is the one best placed to complete it.
        /// </summary>
        private void ReleasePendingHandles()
        {
            if (Disposed && HasNativeHandles)
            {
                ReleaseNativeHandles(PendingReleaseTimeoutInMs);
            }
        }

        /// <summary>
        /// Releases the native handles this instance owns, as far as the locks allow. Each handle is
        /// zeroed under the lock that guards it before being destroyed, so exactly one caller ever frees
        /// it however many callers race here. A handle whose lock couldn't be acquired is left in place
        /// rather than freed unsafely, and the next caller picks it up: a lock timeout must not turn into
        /// a permanent leak. Only meaningful once <see cref="Disposed"/> is set.
        /// </summary>
        /// <param name="timeoutInMs">how long to wait for each of the two locks</param>
        private void ReleaseNativeHandles(int timeoutInMs)
        {
            // Same lock order as Update: builder first, then the WAF write lock.
            var builderLockTaken = Monitor.TryEnter(_builderLock, timeoutInMs);
            try
            {
                // we really need to enter here so longer timeout, otherwise waf handle might not be disposed
                if (_wafLocker.EnterWriteLock(timeoutInMs))
                {
                    // Zeroing under the write lock is what makes this safe to call more than once:
                    // ddwaf_destroy does `delete handle` unconditionally (no native refcount on the
                    // handle itself), so a second destroy would corrupt the heap. The write lock is also
                    // mutually exclusive with the read lock CreateContext holds, so no ddwaf_context_init
                    // can be reading the handle as it goes away.
                    var wafHandle = _wafHandle;
                    _wafHandle = IntPtr.Zero;

                    if (wafHandle != IntPtr.Zero)
                    {
                        _wafLibraryInvoker.Destroy(wafHandle);
                    }

                    _wafLocker.ExitWriteLock();
                }
                else
                {
                    Log.Warning<int>("Couldn't acquire the waf write lock in {Timeout} ms to release the waf instance, it will be released by the next operation that can", timeoutInMs);
                }

                // The builder is guarded by the builder lock alone, so it can be released whether or not
                // the write lock was acquired.
                if (builderLockTaken)
                {
                    var builderHandle = _wafBuilderHandle;
                    _wafBuilderHandle = IntPtr.Zero;

                    if (builderHandle != IntPtr.Zero)
                    {
                        _wafLibraryInvoker.DestroyBuilder(builderHandle);
                    }
                }
                else
                {
                    Log.Warning<int>("Couldn't acquire the waf builder lock in {Timeout} ms to release the builder, it will be released by the update that is holding it", timeoutInMs);
                }
            }
            finally
            {
                if (builderLockTaken)
                {
                    Monitor.Exit(_builderLock);
                }
            }
        }
    }
}
