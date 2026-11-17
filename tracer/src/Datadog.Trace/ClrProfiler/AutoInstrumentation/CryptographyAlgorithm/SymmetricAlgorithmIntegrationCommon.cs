// <copyright file="SymmetricAlgorithmIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CryptographyAlgorithm;

internal static class SymmetricAlgorithmIntegrationCommon
{
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.SymmetricAlgorithm;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymmetricAlgorithmIntegrationCommon));

    internal static void OnSymmetricAlgorithm<TTarget>(TTarget instance)
    {
        var iast = Iast.Iast.Instance;

        if (!iast.Settings.Enabled || instance is null)
        {
            return;
        }

        try
        {
            IastModule.OnCipherAlgorithm(instance.GetType(), IntegrationId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error analyzing SymmetricAlgorithm.");
        }
    }
}
