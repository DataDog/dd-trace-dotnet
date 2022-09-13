// <copyright file="HashAlgorithmIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.IAST;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm
{
    internal class HashAlgorithmIntegrationCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.HashAlgorithm;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HashAlgorithmIntegrationCommon));

        internal static Scope CreateScope(System.Security.Cryptography.HashAlgorithm instance)
        {
            var iast = Datadog.Trace.IAST.IAST.Instance;
            if (!iast.Settings.Enabled || !iast.Settings.InsecureHashingAlgorithmEnabled)
            {
                return null;
            }

            try
            {
                return IastModule.OnHashingAlgorithm(instance.GetType().Name, IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating hash algorithm scope.");
                return null;
            }
        }
    }
}
