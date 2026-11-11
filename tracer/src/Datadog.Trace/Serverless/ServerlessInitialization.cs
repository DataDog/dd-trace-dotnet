// <copyright file="ServerlessInitialization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Serverless;

/// <summary>
/// Initializes process-lifetime instrumentation for supported serverless environments.
/// </summary>
internal static class ServerlessInitialization
{
    internal static void Initialize(Tracer? tracer, AzureInfo? azureInfo = null)
    {
        if (tracer is null)
        {
            return;
        }

        azureInfo ??= AzureInfo.Instance;
        if (!azureInfo.IsAzureContainerAppJob || !tracer.CurrentTraceSettings.Settings.TraceEnabled)
        {
            return;
        }

        tracer.TracerManager.InitializeProcessLifetimeRootSpan(
            () => AzureContainerAppJobLifecycle.TryStart(tracer, azureInfo));
    }
}
