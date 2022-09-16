// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypesManaged;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private const string InitContextError = "WAF ddwaf_init_context failed.";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly IntPtr? ruleHandle;
        private readonly InitializationResult initializationResult;
        private readonly WafNative wafNative;
        private readonly Encoder encoder;
        private bool disposed = false;

        private Waf(InitializationResult initalizationResult, WafNative wafNative, Encoder encoder)
        {
            initializationResult = initalizationResult;
            ruleHandle = initalizationResult.RuleHandle;
            this.wafNative = wafNative;
            this.encoder = encoder;
        }

        ~Waf()
        {
            Dispose(false);
        }

        public bool InitializedSuccessfully => ruleHandle.HasValue;

        public InitializationResult InitializationResult => initializationResult;

        public string Version
        {
            get { return wafNative.GetVersion(); }
        }

        /// <summary>
        /// Loads library and configure it with the ruleset file
        /// </summary>
        /// <param name="obfuscationParameterKeyRegex">the regex that will be used to obfuscate possible sensitive data in keys that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="obfuscationParameterValueRegex">the regex that will be used to obfuscate possible sensitive data in values that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="rulesFile">can be null, means use rules embedded in the manifest </param>
        /// <param name="libVersion">can be null, means use a specific version in the name of the loaded file </param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static Waf Create(string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string rulesFile = null, string libVersion = null)
        {
            var libraryHandle = LibraryLoader.LoadAndGetHandle(libVersion);
            if (libraryHandle == IntPtr.Zero)
            {
                return null;
            }

            var wafNative = new WafNative(libraryHandle);
            var encoder = new Encoder(wafNative);
            var initalizationResult = WafConfigurator.Configure(rulesFile, wafNative, encoder, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
            return new Waf(initalizationResult, wafNative, encoder);
        }

        public IContext CreateContext()
        {
            var contextHandle = wafNative.InitContext(ruleHandle.Value);

            if (contextHandle == IntPtr.Zero)
            {
                Log.Error(InitContextError);
                throw new Exception(InitContextError);
            }

            return new Context(contextHandle, wafNative, encoder);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool UpdateRules(IEnumerable<RuleData> res)
        {
            var finalRuleDatas = MergeRuleDatas(res);
            var encoded = encoder.Encode(finalRuleDatas, new List<Obj>(), false);
            var ret = wafNative.UpdateRuleData(ruleHandle.Value, encoded.RawPtr);
            return ret == DDWAF_RET_CODE.DDWAF_OK;
        }

        internal static List<object> MergeRuleDatas(IEnumerable<RuleData> res)
        {
            var finalRuleDatas = new List<object>();
            var groups = res.GroupBy(r => r.Id + r.Type);
            foreach (var ruleDatas in groups)
            {
                var datasByValue = ruleDatas.SelectMany(d => d.Data).GroupBy(d => d.Value);
                var mergedDatas = new List<object>();
                foreach (var data in datasByValue)
                {
                    var longestLastingIp = data.OrderByDescending(d => d.Expiration ?? long.MaxValue).First();
                    var dataIp = new Dictionary<string, object>();
                    if (longestLastingIp.Expiration.HasValue)
                    {
                        dataIp.Add("expiration", longestLastingIp.Expiration.Value);
                    }

                    dataIp.Add("value", longestLastingIp.Value);
                    mergedDatas.Add(dataIp);
                }

                var ruleData = ruleDatas.First();
                finalRuleDatas.Add(new Dictionary<string, object> { { "id", ruleData.Id }, { "type", ruleData.Type }, { "data", mergedDatas } });
            }

            return finalRuleDatas;
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (ruleHandle.HasValue)
            {
                wafNative.Destroy(ruleHandle.Value);
            }
        }
    }
}
