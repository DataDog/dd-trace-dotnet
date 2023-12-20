// <copyright file="HashAlgorithmIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;

internal class HashAlgorithmIntegrationCommon
{
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.HashAlgorithm;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HashAlgorithmIntegrationCommon));

    internal static Scope? CreateScope<TTarget>(TTarget instance)
    {
        if (instance is System.Security.Cryptography.HashAlgorithm algorithm)
        {
            var iast = Iast.Iast.Instance;
            if (!iast.Settings.Enabled)
            {
                return null;
            }

            try
            {
                return IastModule.OnHashingAlgorithm(GetAlgorithmName(algorithm.GetType()), IntegrationId).SingleSpan;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating hash algorithm scope.");
                return null;
            }
        }

        return null;
    }

    private static string? GetAlgorithmName(Type type)
    {
        return type.BaseType?.BaseType == typeof(System.Security.Cryptography.HashAlgorithm) ? type.BaseType?.Name : type.Name;
    }
}
