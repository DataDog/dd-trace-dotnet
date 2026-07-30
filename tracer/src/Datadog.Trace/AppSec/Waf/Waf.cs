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
        private bool? _isKnowAddressesSuported;

        internal Waf(IntPtr wafBuilderHandle, IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafBuilderHandle = wafBuilderHandle;
            _wafHandle = wafHandle;
            _encoder = encoder;
        }

        public bool Disposed { get; private set; }

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
                var result = wafConfigurator.Configure(configurationStatus, encoder, obfuscationParameterKeyRegex, obfuscationParameterValueRegex, ref diagnostics, configurationStatus.RuleSetTitle);
                var initResult = InitResult.From(ref result);
                if (initResult.Waf is null)
                {
                    // ownership of the native handles is transferred to the Waf instance, so when there
                    // is none (the build failed, or the ruleset had no usable rules) nothing else will
                    // ever release them and they have to be freed here
                    if (result.WafHandle != IntPtr.Zero)
                    {
                        wafLibraryInvoker.Destroy(result.WafHandle);
                    }

                    if (result.WafBuilderHandle != IntPtr.Zero)
                    {
                        wafLibraryInvoker.DestroyBuilder(result.WafBuilderHandle);
                    }
                }

                return initResult;
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
                    Log.Warning("Context can't be created as waf instance has been disposed.");
                    return null;
                }

                contextHandle = _wafLibraryInvoker.InitContext(_wafHandle);
                _wafLocker.ExitReadLock();
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
            if (Disposed)
            {
                return;
            }

            // Take the builder lock first, in the same order as Update does, so that an update which is
            // already building an instance finishes before the builder is destroyed under it.
            var builderLockTaken = Monitor.TryEnter(_builderLock, DisposeTimeoutInMs);
            try
            {
                // we really need to enter here so longer timeout, otherwise waf handle might not be disposed
                if (_wafLocker.EnterWriteLock(DisposeTimeoutInMs))
                {
                    // Set Disposed and Destroy the handle atomically under the write lock. A plain
                    // "Disposed = true" before the lock would let two concurrent Dispose() calls both
                    // pass the outer guard and both reach Destroy(_wafHandle), which is a double free:
                    // ddwaf_destroy does `delete handle` unconditionally (no native refcount on the
                    // handle itself), so a second call corrupts the heap. The inner re-check ensures
                    // exactly one caller destroys the handle.
                    if (!Disposed)
                    {
                        Disposed = true;
                        _wafLibraryInvoker.Destroy(_wafHandle);
                    }

                    _wafLocker.ExitWriteLock();
                }
                else
                {
                    // Couldn't acquire the write lock; mark disposed so other operations bail out,
                    // even though the WAF instance leaks in this (rare) timeout case.
                    Disposed = true;
                }

                // The builder is guarded by the builder lock alone, so it can be released whether or not
                // the write lock was acquired. Zeroing it makes a later Update bail out, and a second
                // Dispose a no-op, instead of handing a destroyed builder to libddwaf.
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
                    Log.Warning<int>("Couldn't acquire the waf builder lock in {Timeout} ms, the builder will leak as an update is still using it", DisposeTimeoutInMs);
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
