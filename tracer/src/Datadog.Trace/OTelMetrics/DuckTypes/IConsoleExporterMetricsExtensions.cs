// <copyright file="IConsoleExporterMetricsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OTelMetrics.DuckTypes
{
    internal interface IConsoleExporterMetricsExtensions : IDuckType
    {
        IMeterProviderBuilder AddConsoleExporter(IMeterProviderBuilder builder);
    }
}
#endif
