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
        public const string AssemblyVersion = "3.46.0.0";
        public const string ThreePartVersion = "3.46.0";

        // ReportedVersion is used for telemetry tags only — not for native/managed version matching.
        // PR CI and local builds get a -dev suffix so monitors can filter them out automatically.
#if DD_CI_BUILD
        public const string ReportedVersion = "3.46.0-dev";
#else
        public const string ReportedVersion = "3.46.0";
#endif

        public static ReadOnlySpan<byte> AssemblyVersionBytes => "3.46.0.0"u8;
    }
}
