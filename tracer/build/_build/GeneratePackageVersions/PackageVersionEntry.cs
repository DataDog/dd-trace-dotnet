// <copyright file="PackageVersionEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace GeneratePackageVersions
{
    public class PackageVersionEntry : IPackageVersionEntry
    {
        /// <summary>
        /// The name of the integration. Must be a valid C# identifier
        /// </summary>
        public string IntegrationName { get; set; }

        /// <summary>
        ///  The sample project that uses the package
        /// </summary>
        public string SampleProjectName { get; set; }

        /// <summary>
        /// The NuGet package to search for. Must be listed in <see cref="Honeypot.IntegrationMap"/>
        /// </summary>
        public string NugetPackageSearchName { get; set; }

        /// <summary>
        /// The minimum version of the NuGet package to use
        /// </summary>
        public string MinVersion { get; set; }

        /// <summary>
        /// The maximum version of the NuGet package to use (exclusive)
        /// </summary>
        public string MaxVersionExclusive { get; set; }

        /// <summary>
        /// Specific versions to use when running select versions.
        /// May use wildcards to indicate "latest" of a particular range
        /// </summary>
        public string[] SpecificVersions { get; set; } = Array.Empty<string>();

        public PackageVersionConditionEntry[] VersionConditions { get; set; } = Array.Empty<PackageVersionConditionEntry>();

        public record PackageVersionConditionEntry
        {
            public string MinVersion { get; init; }
            public string MaxVersionExclusive { get; init; }
            public TargetFramework[] ExcludeTargetFrameworks { get; init; } = Array.Empty<TargetFramework>();
            public TargetFramework[] IncludeOnlyTargetFrameworks { get; init; } = Array.Empty<TargetFramework>();

            /// <summary>
            /// If true, packages in the range will not be built on Arm64
            /// </summary>
            public bool SkipArm64 { get; init; }

            /// <summary>
            /// If true, packages in the range will not built on Alpine
            /// </summary>
            public bool SkipAlpine { get; set; }
        }
    }
}
