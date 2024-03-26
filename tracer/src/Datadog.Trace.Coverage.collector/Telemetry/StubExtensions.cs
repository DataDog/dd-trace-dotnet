// <copyright file="StubExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Telemetry;

/// <summary>
/// Stub extension methods to satisfy expectations of imported code
/// </summary>
internal static class StubExtensions
{
    public static string ToStringFast(this TelemetryErrorCode errorCode) => errorCode.ToString();
}
