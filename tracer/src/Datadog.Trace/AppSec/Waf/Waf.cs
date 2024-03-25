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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly Concurrency.ReaderWriterLock _wafLocker = new();
        private readonly IEncoder _encoder;
        private IntPtr _wafHandle;

        internal Waf(IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker, IEncoder encoder)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafHandle = wafHandle;
            _encoder = encoder;
        }

        internal bool Disposed { get; private set; }

        public string Version => _wafLibraryInvoker.GetVersion();

        /// <summary>
        /// Create a new waf object configured with the ruleset file
        /// </summary>
        /// <param name="wafLibraryInvoker">to invoke native methods on the waf's native library</param>
        /// <param name="obfuscationParameterKeyRegex">the regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="obfuscationParameterValueRegex">the regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="embeddedRulesetPath">can be null, means use rules embedded in the manifest </param>
        /// <param name="rulesFromRcm">can be null. RemoteConfig rules json. Takes precedence over rulesFile </param>
        /// <param name="setupWafSchemaExtraction">should we read the config file for schema extraction</param>
        /// <param name="useUnsafeEncoder">use legacy encoder</param>
        /// <param name="wafDebugEnabled">if debug level logs should be enabled for the WAF</param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static unsafe InitResult Create(
            WafLibraryInvoker wafLibraryInvoker,
            string obfuscationParameterKeyRegex,
            string obfuscationParameterValueRegex,
            string? embeddedRulesetPath = null,
            JToken? rulesFromRcm = null,
            bool setupWafSchemaExtraction = false,
            bool useUnsafeEncoder = false,
            bool wafDebugEnabled = false)
        {
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);

            // set the log level and setup the logger
            wafLibraryInvoker.SetupLogging(wafDebugEnabled);

            var jtokenRoot = rulesFromRcm ?? WafConfigurator.DeserializeEmbeddedOrStaticRules(embeddedRulesetPath)!;
            if (jtokenRoot is null)
            {
                return InitResult.FromUnusableRuleFile();
            }

            DdwafObjectStruct rulesObj;
            DdwafConfigStruct configWafStruct = default;
            IEncodeResult? result = null;
            IEncoder encoder;
            var keyRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterKeyRegex);
            var valueRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterValueRegex);
            configWafStruct.KeyRegex = keyRegex;
            configWafStruct.ValueRegex = valueRegex;
            // here we decide not to configure any free function like `configWafStruct.FreeWafFunction = wafLibraryInvoker.ObjectFreeFuncPtr`
            // as we free the object ourselves in both cases calling for the legacy encoder wafLibraryInvoker.ObjectFreeFuncPtr manually and for the other ones, handling our own allocations
            if (useUnsafeEncoder)
            {
                encoder = new Encoder();
                result = encoder.Encode(jtokenRoot, applySafetyLimits: false);
                rulesObj = result.ResultDdwafObject;
            }
            else
            {
                encoder = new EncoderLegacy(wafLibraryInvoker);
                var configObjWrapper = encoder.Encode(jtokenRoot, applySafetyLimits: false);
                rulesObj = configObjWrapper.ResultDdwafObject;
            }

            var diagnostics = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };

            try
            {
                var initResult = wafConfigurator.Configure(rulesObj, encoder, configWafStruct, ref diagnostics, rulesFromRcm == null ? embeddedRulesetPath : "RemoteConfig");
                initResult.EmbeddedRules = jtokenRoot;
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

                wafLibraryInvoker.ObjectFreePtr((IntPtr)(&diagnostics));

                if (useUnsafeEncoder)
                {
                    result?.Dispose();
                }
            }
        }

        private unsafe UpdateResult UpdateWafAndDispose(IEncodeResult updateData)
        {
            UpdateResult? res = null;
            var diagnosticsValue = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
            try
            {
                var newHandle = _wafLibraryInvoker.Update(_wafHandle, ref updateData.ResultDdwafObject, ref diagnosticsValue);
                if (newHandle != IntPtr.Zero)
                {
                    var oldHandle = _wafHandle;
                    if (_wafLocker.EnterWriteLock())
                    {
                        _wafHandle = newHandle;
                        _wafLocker.ExitWriteLock();
                        _wafLibraryInvoker.Destroy(oldHandle);
                        return new(diagnosticsValue, true);
                    }

                    _wafLibraryInvoker.Destroy(newHandle);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "An exception occurred while trying to update waf with new data");
            }
            finally
            {
                res ??= new(diagnosticsValue, false);
                _wafLibraryInvoker.ObjectFreePtr((IntPtr)(&diagnosticsValue));
                updateData.Dispose();
            }

            return res;
        }

        public UpdateResult UpdateWafFromConfigurationStatus(ConfigurationStatus configurationStatus)
        {
            var dic = configurationStatus.BuildDictionaryForWafAccordingToIncomingUpdate();
            return Update(dic);
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

        private UpdateResult Update(IDictionary<string, object> arguments)
        {
            UpdateResult updated;
            try
            {
                var encodedArgs = _encoder.Encode(arguments, applySafetyLimits: false);
                updated = UpdateWafAndDispose(encodedArgs);

                // only if rules are provided will the waf give metrics
                if (arguments.ContainsKey("rules"))
                {
                    TelemetryFactory.Metrics.RecordCountWafUpdates();
                }
            }
            catch
            {
                updated = UpdateResult.FromUnusableRules();
            }

            return updated;
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
            _wafLibraryInvoker.Destroy(_wafHandle);
            _wafLocker.Dispose();
        }
    }
}
