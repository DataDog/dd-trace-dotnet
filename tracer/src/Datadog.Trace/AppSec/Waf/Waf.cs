// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly WafConfigurator _wafConfigurator;
        private IntPtr _wafHandle;

        internal Waf(IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafHandle = wafHandle;
            _wafConfigurator = new WafConfigurator(_wafLibraryInvoker);
        }

        ~Waf()
        {
            Dispose(false);
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
        /// <param name="rulesFile">can be null, means use rules embedded in the manifest </param>
        /// <param name="rulesJson">can be null. RemoteConfig rules json. Takes precedence over rulesFile </param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static InitializationResult Create(WafLibraryInvoker wafLibraryInvoker, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string? rulesFile = null, string? rulesJson = null)
        {
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);
            InitializationResult initializationResult;
            if (!string.IsNullOrEmpty(rulesJson))
            {
                initializationResult = wafConfigurator.ConfigureFromRemoteConfig(rulesJson, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            }
            else
            {
                initializationResult = wafConfigurator.Configure(rulesFile, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            }

            return initializationResult;
        }

        public bool UpdateRules(string rules)
        {
            var argCache = new List<Obj>();
            using var rulesObj = _wafConfigurator.GetConfigObjFromRemoteJson(rules, argCache);
            var rulesetInfo = default(DdwafRuleSetInfoStruct);
            var result = _wafLibraryInvoker.Update(_wafHandle, rulesObj.RawPtr, ref rulesetInfo);
            return UpdateWafHandle(result);
        }

        private bool UpdateWafHandle(IntPtr result)
        {
            if (result != IntPtr.Zero)
            {
                var oldHandle = _wafHandle;
                _wafHandle = result;
                _wafLibraryInvoker.Destroy(oldHandle);
                return true;
            }

            return false;
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

            var contextHandle = _wafLibraryInvoker.InitContext(_wafHandle);

            if (contextHandle == IntPtr.Zero)
            {
                Log.Error(InitContextError);
                throw new Exception(InitContextError);
            }

            return Context.GetContext(contextHandle, this, _wafLibraryInvoker);
        }

        // Requires a non disposed waf handle
        public bool UpdateRulesData(List<RuleData> rulesData)
        {
            if (Disposed)
            {
                Log.Warning("Waf instance has been disposed when trying to update rules data");
                return false;
            }

            if (rulesData.Count == 0)
            {
                return false;
            }

            var mergedRuleData = MergeRuleData(rulesData);
            using var encoded = mergedRuleData.Encode(_wafLibraryInvoker);
            DdwafRuleSetInfoStruct ruleSetInfo = default;
            var result = _wafLibraryInvoker.Update(_wafHandle, encoded.RawPtr, ref ruleSetInfo);
            var updated = UpdateWafHandle(result);
            Log.Information("{Number} rules have been updated and waf has been updated: {Updated}", mergedRuleData.Count, updated);
            return updated;
        }

        /// <summary>
        /// Requires a non disposed waf handle
        /// </summary>
        /// <param name="ruleStatus">whether rules have been toggled</param>
        /// <returns>whether or not rules were toggled</returns>
        public bool UpdateRulesStatus(List<RuleOverride> ruleStatus)
        {
            if (Disposed)
            {
                Log.Warning("Waf instance has been disposed when trying to toggle rules");
                return false;
            }

            if (ruleStatus is not { Count: not 0 })
            {
                return false;
            }

            using var encoded = ruleStatus.Encode(_wafLibraryInvoker);
            var ruleSetInfo = default(DdwafRuleSetInfoStruct);
            var result = _wafLibraryInvoker.Update(_wafHandle, encoded.RawPtr, ref ruleSetInfo);
            var updated = UpdateWafHandle(result);
            Log.Information("{Number} rule status have been updated and waf has been updated: {Updated}", ruleStatus.Count, updated);
            return updated;
        }

        // Doesn't require a non disposed waf handle, but as the WAF instance needs to be valid for the lifetime of the context, if waf is disposed, don't run (unpredictable)
        public DDWAF_RET_CODE Run(IntPtr contextHandle, IntPtr rawArgs, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds) => _wafLibraryInvoker.Run(contextHandle, rawArgs, ref retNative, timeoutMicroSeconds);

        internal static List<RuleData> MergeRuleData(IEnumerable<RuleData> res)
        {
            if (res == null)
            {
                throw new ArgumentNullException(nameof(res));
            }

            var finalRuleDatas = new List<RuleData>();
            var groups = res.GroupBy(r => r.Id + r.Type);
            foreach (var ruleDatas in groups)
            {
                var datasByValue = ruleDatas.SelectMany(d => d.Data!).GroupBy(d => d.Value);
                var mergedDatas = new List<Data>();
                foreach (var data in datasByValue)
                {
                    var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                    mergedDatas.Add(longestLastingIp);
                }

                var ruleData = ruleDatas.FirstOrDefault();
                if (ruleData != null && !string.IsNullOrEmpty(ruleData.Type) && !string.IsNullOrEmpty(ruleData.Id))
                {
                    ruleData.Data = mergedDatas.ToArray();
                    finalRuleDatas.Add(ruleData);
                }
            }

            return finalRuleDatas;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
            _wafLibraryInvoker.Destroy(_wafHandle);
        }
    }
}
