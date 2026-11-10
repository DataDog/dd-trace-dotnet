// <copyright file="OpenTelemetryConfigurationHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration.ConfigurationSources;

internal static class OpenTelemetryConfigurationHelpers
{
    /// <summary>
    /// Returns true when <paramref name="value"/> is null, or when <paramref name="key"/> is an OTEL_* setting
    /// and <paramref name="value"/> is empty or whitespace-only.
    /// The OpenTelemetry spec requires an empty value to be treated as unset
    /// https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/#parsing-empty-value
    /// and the upstream OpenTelemetry .NET SDK also treats whitespace-only values as unset.
    /// </summary>
    internal static bool IsUnset(string key, [NotNullWhen(false)] string? value)
        => value is null
        || (StringUtil.IsNullOrWhiteSpace(value) && key.StartsWith("OTEL_", StringComparison.Ordinal));
}
