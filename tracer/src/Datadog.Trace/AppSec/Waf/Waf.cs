// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly Concurrency.ReaderWriterLock _wafLocker = new();
        private readonly IEncoder _encoder;
        private IntPtr _wafBuilderHandle;
        private IntPtr _wafHandle;

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
            DdwafConfigStruct configWafStruct = default;
            var keyRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterKeyRegex);
            var valueRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterValueRegex);
            configWafStruct.KeyRegex = keyRegex;
            configWafStruct.ValueRegex = valueRegex;

            var diagnostics = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);
            try
            {
                var initResult = wafConfigurator.Configure(configurationStatus, encoder, ref configWafStruct, ref diagnostics, configurationStatus.RuleSetTitle);
                return initResult;
            }
            finally
            {
                if (keyRegex != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(keyRegex);
                }

                if (valueRegex != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valueRegex);
                }

                wafLibraryInvoker.ObjectFree(ref diagnostics);
            }
        }

        public bool IsKnowAddressesSuported()
        {
            return _wafLibraryInvoker.IsKnowAddressesSuported();
        }

        public string[] GetKnownAddresses()
        {
            bool lockAcquired = false;
            try
            {
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

        public UpdateResult Update(ConfigurationState configurationStatus)
        {
            if (Disposed)
            {
                // Early bail out with no lock
                return UpdateResult.FromFailed("Waf is already disposed and can't be updated");
            }

            var diagnostics = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
            var wafConfigurator = new WafConfigurator(_wafLibraryInvoker);
            try
            {
                var updateResult = wafConfigurator.Update(_wafBuilderHandle, configurationStatus, _encoder, ref diagnostics, configurationStatus.RuleSetTitle);
                if (!updateResult.Success || updateResult.WafHandle == _wafHandle || updateResult.WafHandle == IntPtr.Zero)
                {
                    Log.Warning("A waf update came from remote configuration but final merged dictionary for waf is empty, no update will be performed.");
                }
                else
                {
                    if (_wafLocker.EnterWriteLock())
                    {
                        if (!Disposed)
                        {
                            // update within the lock as iis can recycle and cause dispose to happen at the same time
                            var newHandle = updateResult.WafHandle;
                            var oldHandle = _wafHandle;
                            _wafHandle = newHandle;
                            _wafLocker.ExitWriteLock();
                            _wafLibraryInvoker.Destroy(oldHandle);
                        }
                        else
                        {
                            _wafLocker.ExitWriteLock();
                            return UpdateResult.FromFailed("Waf is already disposed and can't be updated");
                        }
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
            catch
            {
                TelemetryFactory.Metrics.RecordCountWafUpdates(Telemetry.Metrics.MetricTags.WafStatus.Error);
                return UpdateResult.FromUnusableRules();
            }
            finally
            {
                _wafLibraryInvoker.ObjectFree(ref diagnostics);
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
        public unsafe WafReturnCode Run(IntPtr contextHandle, DdwafObjectStruct* rawPersistentData, DdwafObjectStruct* rawEphemeralData, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds)
            => _wafLibraryInvoker.Run(contextHandle, rawPersistentData, rawEphemeralData, ref retNative, timeoutMicroSeconds);

        internal static List<RuleData> MergeRuleData(IEnumerable<RuleData> res)
        {
            if (res == null)
            {
                throw new ArgumentNullException(nameof(res));
            }

            var finalRuleData = new List<RuleData>();
            var groups = res.GroupBy(r => r.Id + r.Type);
            foreach (var ruleDatas in groups)
            {
                var dataByValue = ruleDatas.SelectMany(d => d.Data!).GroupBy(d => d.Value);
                var mergedDatas = new List<Data>();
                foreach (var data in dataByValue)
                {
                    var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                    mergedDatas.Add(longestLastingIp);
                }

                var ruleData = ruleDatas.FirstOrDefault();
                if (ruleData != null && !string.IsNullOrEmpty(ruleData.Type) && !string.IsNullOrEmpty(ruleData.Id))
                {
                    ruleData.Data = mergedDatas.ToArray();
                    finalRuleData.Add(ruleData);
                }
            }

            return finalRuleData;
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
            // we really need to enter here so longer timeout, otherwise waf handle might not be disposed
            if (_wafLocker.EnterWriteLock(15000))
            {
                _wafLibraryInvoker.Destroy(_wafHandle);
                _wafLocker.ExitWriteLock();
            }
        }
    }
}
