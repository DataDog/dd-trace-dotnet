// <copyright file="DefinitionPaths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.Configurations.Models;

internal static class DefinitionPaths
{
    public const string MetricProbe = "metricProbe_";
    public const string SpanDecorationProbe = "spanDecorationProbe_";
    public const string LogProbe = "logProbe_";
    public const string SpanProbe = "spanProbe_";
    public const string ServiceConfiguration = "serviceConfig_";
    public const string SymDB = "symDb";
}
