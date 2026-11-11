// <copyright file="AzureContainerAppJobLifecycle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Serverless;

/// <summary>
/// Owns the process-lifetime execution span for an Azure Container Apps Job worker.
/// </summary>
internal sealed class AzureContainerAppJobLifecycle : IProcessLifetimeRootSpan
{
    internal const string OperationName = "azure.container_apps.job";

    private const string ComponentName = "AzureContainerAppsJobs";
    private const string ProcessExitCodeTag = "process.exit.code";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureContainerAppJobLifecycle>();

    private readonly Scope _scope;
    private int _completed;

    private AzureContainerAppJobLifecycle(Scope scope)
    {
        _scope = scope;
    }

    internal Span Span => _scope.Span;

    public SpanContext? SpanContext => Volatile.Read(ref _completed) == 0 ? _scope.Span.Context : null;

    /// <summary>
    /// Starts one root span when both Azure Container Apps Job identity variables are present.
    /// The scope is activated on the tracer-startup execution context so subsequent automatically
    /// instrumented work in that context is parented beneath the Job execution.
    /// </summary>
    internal static AzureContainerAppJobLifecycle? TryStart(Tracer tracer, AzureInfo azureInfo)
    {
        if (!azureInfo.IsAzureContainerAppJob || !tracer.CurrentTraceSettings.Settings.TraceEnabled)
        {
            return null;
        }

        try
        {
            var jobName = azureInfo.AzureContainerAppJobName;
            var executionName = azureInfo.AzureContainerAppJobExecutionName;
            var tags = new TagsList();
            tags.SetTags(
                new KeyValuePair<string, string?>(Tags.SpanKind, SpanKinds.Internal),
                new KeyValuePair<string, string?>(Tags.InstrumentationName, ComponentName),
                new KeyValuePair<string, string?>(Tags.AzureContainerAppJobName, jobName),
                new KeyValuePair<string, string?>(Tags.AzureContainerAppJobExecutionName, executionName));

            // Container Apps Jobs are finite serverless executions. Use the established .NET
            // serverless span type instead of introducing the worker type used by some tracers.
            var scope = tracer.StartActiveInternal(OperationName, parent: SpanContext.None, tags: tags);
            scope.Span.ResourceName = jobName;
            scope.Span.Type = SpanTypes.Serverless;

            return new AzureContainerAppJobLifecycle(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating the Azure Container Apps Job execution span");
            return null;
        }
    }

    /// <summary>
    /// Finishes the execution span before the tracer performs its final writer flush.
    /// </summary>
    public void Complete(Exception? exception)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        try
        {
            var exitCode = Environment.ExitCode;
            _scope.Span.SetTag(ProcessExitCodeTag, exitCode.ToString(CultureInfo.InvariantCulture));

            if (exception is not null)
            {
                _scope.Span.SetException(exception);
            }
            else if (exitCode != 0)
            {
                _scope.Span.Error = true;
            }

            _scope.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error completing the Azure Container Apps Job execution span");
        }
    }
}
