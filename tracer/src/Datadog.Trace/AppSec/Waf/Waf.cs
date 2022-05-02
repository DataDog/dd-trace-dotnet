// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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

        public Version Version
        {
            get
            {
                var ver = wafNative.GetVersion();
                return new Version(ver.Major, ver.Minor, ver.Patch);
            }
        }

        /// <summary>
        /// Loads library and configure it with the ruleset file
        /// </summary>
        /// <param name="obfuscationParameterKeyRegex">the regex that will be used to obfuscate possible senative data in keys that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="obfuscationParameterValueRegex">the regex that will be used to obfuscate possible senative data in values that are highlighted WAF as potentially malicious,
        /// empty string means use default embedded in the WAF</param>
        /// <param name="rulesFile">can be null, means use rules embedded in the manifest </param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static Waf Create(string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex, string rulesFile = null)
        {
            var libraryHandle = LibraryLoader.LoadAndGetHandle();
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
            var contextHandle = wafNative.InitContext(ruleHandle.Value, wafNative.ObjectFreeFuncPtr);

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
