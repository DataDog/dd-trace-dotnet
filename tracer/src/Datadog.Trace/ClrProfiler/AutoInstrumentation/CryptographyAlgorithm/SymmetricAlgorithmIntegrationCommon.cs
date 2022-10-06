// <copyright file="SymmetricAlgorithmIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CryptographyAlgorithm
{
    internal class SymmetricAlgorithmIntegrationCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.SymmetricAlgorithm;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymmetricAlgorithmIntegrationCommon));

        internal static Scope? CreateScope(SymmetricAlgorithm instance)
        {
            var iast = Iast.Iast.Instance;
            if (!iast.Settings.Enabled || instance == null)
            {
                return null;
            }

            try
            {
                return IastModule.OnCipherAlgorithm(instance.GetType()?.BaseType?.Name, IntegrationId, iast);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating SymmetricAlgorithm scope.");
                return null;
            }
        }
    }
}
