// <copyright file="HangfireConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// Class to contain reused strings for Hangfire Instrumentation
/// </summary>
internal static class HangfireConstants
{
    internal const string OnPerformOperation = "hangfire.perform";
    internal const string DatadogScopeKey = "datadog_scope_key";
    internal const string DatadogContextKey = "datadog_context_key";
    internal const string JobIdTag = "job.id";
    internal const string JobCreatedAtTag = "job.createdat";
    internal const string ResourceNamePrefix = "job ";
}
