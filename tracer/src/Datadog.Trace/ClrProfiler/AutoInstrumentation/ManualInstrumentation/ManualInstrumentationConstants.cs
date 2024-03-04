// <copyright file="ManualInstrumentationConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

internal class ManualInstrumentationConstants
{
    public const string MinVersion = "3.0.0";
    public const string MaxVersion = "3.*.*";
    public const string IntegrationName = nameof(IntegrationId.DatadogTraceManual);
    public const IntegrationId Id = IntegrationId.DatadogTraceManual;
}
