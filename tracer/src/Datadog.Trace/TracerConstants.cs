// <copyright file="TracerConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    internal static class TracerConstants
    {
        public const string TelemetrySdkName = "datadog";
        public const string Language = "dotnet";

        public const string AssemblyVersion = ProjectInfo.AssemblyVersion;

        // Instrumentation.cs compares this against the native tracer's PROFILER_VERSION for an exact
        // string match, so it must stay a bare numeric three-part version.
        public const string ThreePartVersion = ProjectInfo.ThreePartVersion;

        public static ReadOnlySpan<byte> AssemblyVersionBytes => ProjectInfo.AssemblyVersionBytes;
    }
}
