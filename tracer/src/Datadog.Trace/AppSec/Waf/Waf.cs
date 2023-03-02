// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafLibraryInvoker _wafLibraryInvoker;
        private readonly WafConfigurator _wafConfigurator;
        private readonly Concurrency.ReaderWriterLock _wafLocker = new();
        private IntPtr _wafHandle;

        internal Waf(IntPtr wafHandle, WafLibraryInvoker wafLibraryInvoker)
        {
            _wafLibraryInvoker = wafLibraryInvoker;
            _wafHandle = wafHandle;
            _wafConfigurator = new WafConfigurator(_wafLibraryInvoker);
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
        internal static InitResult Create(WafLibraryInvoker wafLibraryInvoker, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string? rulesFile = null, string? rulesJson = null)
        {
            var wafConfigurator = new WafConfigurator(wafLibraryInvoker);
            InitResult initResult;
            if (!string.IsNullOrEmpty(rulesJson))
            {
                initResult = wafConfigurator.ConfigureFromRemoteConfig(rulesJson!, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            }
            else
            {
                initResult = wafConfigurator.Configure(rulesFile, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            }

            return initResult;
        }

        private UpdateResult UpdateWafAndDisposeItems(Obj updateData, IEnumerable<Obj> argsToDispose, DdwafRuleSetInfo? ruleSetInfo = null)
        {
            try
            {
                var newHandle = _wafLibraryInvoker.Update(_wafHandle, updateData.RawPtr, ruleSetInfo);

                if (newHandle != IntPtr.Zero)
                {
                    var oldHandle = _wafHandle;
                    if (_wafLocker.EnterWriteLock())
                    {
                        _wafHandle = newHandle;
                        _wafLocker.ExitWriteLock();
                        _wafLibraryInvoker.Destroy(oldHandle);
                        return new UpdateResult(ruleSetInfo, true);
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
                if (ruleSetInfo != null)
                {
                    _wafLibraryInvoker.RuleSetInfoFree(ruleSetInfo);
                }

                _wafLibraryInvoker.ObjectFreePtr(updateData.RawPtr);
                updateData.Dispose();

                foreach (var arg in argsToDispose)
                {
                    arg.Dispose();
                }
            }

            return new UpdateResult(ruleSetInfo, false);
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

        public UpdateResult UpdateRules(string rules)
        {
            var rulesetInfo = new DdwafRuleSetInfo();
            var argsToDispose = new List<Obj>();
            var rulesObj = _wafConfigurator.GetConfigObjFromRemoteJson(rules, argsToDispose);
            if (rulesObj == null)
            {
                return UpdateResult.FromUnusableRules();
            }

            var updated = UpdateWafAndDisposeItems(rulesObj, argsToDispose, rulesetInfo);
            return updated;
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
                return true;
            }

            var argsToDispose = new List<Obj>();
            var mergedRuleData = MergeRuleData(rulesData);
            var rulesDataEncoded = mergedRuleData.Encode(_wafLibraryInvoker, argsToDispose);
            var updated = UpdateWafAndDisposeItems(rulesDataEncoded, argsToDispose);
            Log.Information("{Number} rules have been updated and waf has been updated: {Updated}", mergedRuleData.Count, updated);
            return updated.Success;
        }

        /// <summary>
        /// Requires a non disposed waf handle
        /// </summary>
        /// <param name="ruleStatus">whether rules have been toggled</param>
        /// <param name="exclusions">exclusions</param>
        /// <returns>whether or not rules were toggled</returns>
        public bool UpdateRulesStatus(List<RuleOverride> ruleStatus, List<JToken> exclusions)
        {
            if (Disposed)
            {
                Log.Warning("Waf instance has been disposed when trying to toggle rules");
                return false;
            }

            if (ruleStatus.Count == 0)
            {
                return true;
            }

            var argsToDispose = new List<Obj>();
            var ruleStatusEncoded = EncoderExtensions.Encode(ruleStatus, exclusions, _wafLibraryInvoker, argsToDispose);
            var updated = UpdateWafAndDisposeItems(ruleStatusEncoded, argsToDispose);
            Log.Information("{Number} rule override have been updated and waf has been updated: {Updated}", ruleStatus.Count, updated);
            return updated.Success;
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
