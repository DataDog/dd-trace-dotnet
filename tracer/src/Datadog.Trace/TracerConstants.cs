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

        /// <summary>
        /// The four-part version of Datadog.Trace's strong name. It is the same for a prerelease and the release
        /// with the same X.Y.Z, so don't report it: use <see cref="SemanticVersion"/> instead.
        /// </summary>
        public const string AssemblyVersion = ProjectInfo.AssemblyVersion;

        /// <summary>
        /// The version to report everywhere: X.Y.Z for releases, X.Y.Z-prerelease for prereleases.
        /// Unlike <see cref="AssemblyVersion"/>, it distinguishes a prerelease from the release with the same X.Y.Z.
        /// Instrumentation.cs compares it against the native tracer's PROFILER_VERSION to detect mismatches.
        /// </summary>
        public const string SemanticVersion = ProjectInfo.SemanticVersion;

        public static ReadOnlySpan<byte> SemanticVersionBytes => ProjectInfo.SemanticVersionBytes;
    }
}
