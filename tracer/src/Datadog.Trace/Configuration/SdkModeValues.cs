// <copyright file="SdkModeValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

/// <summary>
/// Valid values of <see cref="ConfigurationKeys.SdkMode"/>.
/// </summary>
internal static class SdkModeValues
{
    public const string Datadog = "datadog";

    public const string Otel = "otel";
}
