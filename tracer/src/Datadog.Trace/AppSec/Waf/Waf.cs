// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypesManaged;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly IntPtr wafHandle;
        private readonly WafNative wafNative;
        private readonly Encoder encoder;

        internal Waf(IntPtr wafHandle, WafNative wafNative, Encoder encoder)
        {
            this.wafHandle = wafHandle;
            this.wafNative = wafNative;
            this.encoder = encoder;
        }

        ~Waf()
        {
            Dispose(false);
        }

        internal bool Disposed { get; private set; }

        public string Version => wafNative.GetVersion();

        public Encoder Encoder => encoder;

        /// <summary>
        /// Loads library and configure it with the ruleset file
        /// </summary>
        /// <param name="obfuscationParameterKeyRegex">the regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="obfuscationParameterValueRegex">the regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="rulesFile">can be null, means use rules embedded in the manifest </param>
        /// <param name="rulesJson">can be null. RemoteConfig rules json. Takes precedence over rulesFile </param>
        /// <param name="libVersion">can be null, means use a specific version in the name of the loaded file </param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static InitializationResult Create(string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string? rulesFile = null, string? rulesJson = null, string? libVersion = null)
        {
            var libraryHandle = LibraryLoader.LoadAndGetHandle(libVersion);
            if (libraryHandle == IntPtr.Zero)
            {
                return InitializationResult.FromLibraryLoadedWrong();
            }

            return InitializationResult.From(libraryHandle, rulesJson, rulesFile, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
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
                Log.Warning("Context can't be create as waf instance has been disposed.");
                return null;
            }

            var contextHandle = wafNative.InitContext(wafHandle);

            if (contextHandle == IntPtr.Zero)
            {
                Log.Error(InitContextError);
                throw new Exception(InitContextError);
            }

            return new Context(contextHandle, this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Requires a non disposed waf handle
        public bool UpdateRulesData(IEnumerable<RuleData> res)
        {
            if (Disposed)
            {
                Log.Warning("Waf instance has been disposed when trying to update rules data");
                return false;
            }

            if (res.Count() == 0)
            {
                return false;
            }

            var finalRuleDatas = MergeRuleData(res);
            using var encoded = encoder.Encode(finalRuleDatas, new List<Obj>(), false);
            var ret = wafNative.UpdateRuleData(wafHandle, encoded.RawPtr);
            Log.Information("{number} rules have been updated and waf status is {status}", finalRuleDatas.Count, ret);
            return ret == DDWAF_RET_CODE.DDWAF_OK;
        }

        /// <summary>
        /// Requires a non disposed waf handle
        /// </summary>
        /// <param name="ruleStatus">whether rules have been toggled</param>
        /// <returns>whether or not rules were toggled</returns>
        public bool ToggleRules(IDictionary<string, bool> ruleStatus)
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

            using var encoded = encoder.Encode(ruleStatus);
            var ret = wafNative.ToggleRules(wafHandle, encoded.RawPtr);
            Log.Information("{number} rule status have been updated and waf status is {status}", ruleStatus.Count, ret);
            return ret == DDWAF_RET_CODE.DDWAF_OK;
        }

        // Doesn't require a non disposed waf handle, but as the WAF instance needs to be valid for the lifetime of the context, if waf is disposed, don't run (unpredictable)
        public DDWAF_RET_CODE Run(IntPtr contextHandle, IntPtr rawArgs, ref DdwafResultStruct retNative, ulong timeoutMicroSeconds) => wafNative.Run(contextHandle, rawArgs, ref retNative, timeoutMicroSeconds);

        public void ResultFree(ref DdwafResultStruct retNative) => wafNative.ResultFree(ref retNative);

        public void ContextDestroy(IntPtr contextHandle) => wafNative.ContextDestroy(contextHandle);

        internal static List<object> MergeRuleData(IEnumerable<RuleData> res)
        {
            if (res == null)
            {
                throw new ArgumentNullException(nameof(res));
            }

            var finalRuleDatas = new List<object>();
            var groups = res.GroupBy(r => r.Id + r.Type);
            foreach (var ruleDatas in groups)
            {
                var datasByValue = ruleDatas.SelectMany(d => d.Data!).GroupBy(d => d.Value);
                var mergedDatas = new List<object>();
                foreach (var data in datasByValue)
                {
                    var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                    var dataIp = new Dictionary<string, object>();
                    if (longestLastingIp.Expiration.HasValue)
                    {
                        dataIp.Add("expiration", longestLastingIp.Expiration.Value);
                    }

                    dataIp.Add("value", longestLastingIp.Value!);
                    mergedDatas.Add(dataIp);
                }

                var ruleData = ruleDatas.FirstOrDefault();
                if (ruleData != null && !string.IsNullOrEmpty(ruleData.Type) && !string.IsNullOrEmpty(ruleData.Id))
                {
                    finalRuleDatas.Add(new Dictionary<string, object> { { "id", ruleData.Id! }, { "type", ruleData.Type! }, { "data", mergedDatas } });
                }
            }

            return finalRuleDatas;
        }

        private void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
            wafNative.Destroy(wafHandle);
        }
    }
}
