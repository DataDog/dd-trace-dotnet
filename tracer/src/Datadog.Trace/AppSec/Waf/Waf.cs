// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
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
        private IntPtr _wafHandle;

        internal Waf(IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafHandle = wafHandle;
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
        /// <returns>the waf wrapper around waf native</returns>
        internal static InitResult Create(WafLibraryInvoker wafLibraryInvoker, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string? embeddedRulesetPath = null, JToken? rulesFromRcm = null, bool setupWafSchemaExtraction = false)
        {
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);
            var isCompatible = wafConfigurator.CheckVersionCompatibility();
            if (!isCompatible)
            {
                return InitResult.FromIncompatibleWaf();
            }

            // set the log level and setup the logger
            wafLibraryInvoker.SetupLogging(GlobalSettings.Instance.DebugEnabledInternal);

            var jtokenRoot = rulesFromRcm ?? WafConfigurator.DeserializeEmbeddedOrStaticRules(embeddedRulesetPath)!;
            if (setupWafSchemaExtraction)
            {
                var schemaConfig = WafConfigurator.DeserializeSchemaExtractionConfig();
                jtokenRoot.Children().Last().AddAfterSelf(schemaConfig!.Children());
            }

            var configObj = Encoder.Encode(jtokenRoot, applySafetyLimits: false);

            var initResult = wafConfigurator.ConfigureAndDispose(configObj.Result, rulesFromRcm != null ? embeddedRulesetPath : "RemoteConfig", obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            initResult.EmbeddedRules = jtokenRoot;
            return initResult;
        }

        private UpdateResult UpdateWafAndDisposeItems(DdwafObjectStruct updateData)
        {
            UpdateResult? res = null;
            DdwafObjectStruct? diagnostics = null;
            try
            {
                diagnostics = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };
                var diagnosticsValue = diagnostics.Value;
                var newHandle = _wafLibraryInvoker.Update(_wafHandle, ref updateData, ref diagnosticsValue);
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
                res ??= new(diagnostics, false);

                if (diagnostics?.Array != IntPtr.Zero)
                {
                    var diagValue = diagnostics!.Value;
                    _wafLibraryInvoker.ObjectFreePtr(ref diagValue.Array);
                }
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
                Log.Warning("Context couldn't be created as we couldnt acquire a reader lock");
                return null;
            }

            if (contextHandle == IntPtr.Zero)
            {
                Log.Error(InitContextError);
                throw new Exception(InitContextError);
            }

            return Context.GetContext(contextHandle, this, _wafLibraryInvoker);
        }

        public UpdateResult Update(IDictionary<string, object> arguments)
        {
            UpdateResult updated;
            try
            {
                using var encodedArgs = Encoder.Encode(arguments, applySafetyLimits: false);

                // only if rules are provided will the waf give metrics
                if (arguments.ContainsKey("rules"))
                {
                    TelemetryFactory.Metrics.RecordCountWafUpdates();
                }

                updated = UpdateWafAndDisposeItems(encodedArgs.Result);
            }
            catch
            {
                updated = UpdateResult.FromUnusableRules();
            }

            return updated;
        }

        // Doesn't require a non disposed waf handle, but as the WAF instance needs to be valid for the lifetime of the context, if waf is disposed, don't run (unpredictable)
        public WafReturnCode Run(IntPtr contextHandle, ref DdwafObjectStruct args, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds) => _wafLibraryInvoker.Run(contextHandle, ref args, ref retNative, timeoutMicroSeconds);

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
