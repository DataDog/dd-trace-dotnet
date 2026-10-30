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

        public const string ThreePartVersion = ProjectInfo.ThreePartVersion;

        /// <summary>
        /// The version to report everywhere: X.Y.Z for releases, X.Y.Z-prerelease for prereleases.
        /// Unlike <see cref="AssemblyVersion"/>, it distinguishes a prerelease from the release with the same X.Y.Z.
        /// Instrumentation.cs compares it against the native tracer's PROFILER_VERSION to detect mismatches.
        /// </summary>
        public const string SemanticVersion = ProjectInfo.SemanticVersion;

        public static ReadOnlySpan<byte> AssemblyVersionBytes => ProjectInfo.AssemblyVersionBytes;

        public static ReadOnlySpan<byte> SemanticVersionBytes => ProjectInfo.SemanticVersionBytes;
    }
}
