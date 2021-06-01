// <copyright file="TracerVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Core.Tools
{
    /// <summary>
    /// The canonical version for the dd-trace-dotnet libraries and tools.
    /// </summary>
    public class TracerVersion
    {
        /// <summary>
        /// The major portion of the current version.
        /// </summary>
        public const int Major = 1;

        /// <summary>
        /// The minor portion of the current version.
        /// </summary>
        public const int Minor = 27;

        /// <summary>
        /// The patch portion of the current version.
        /// </summary>
        public const int Patch = 0;

        /// <summary>
        /// Whether the current release is a pre-release
        /// </summary>
        public const bool IsPreRelease = false;
    }
}
