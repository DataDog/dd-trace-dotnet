// <copyright file="PackageVersionEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace GeneratePackageVersions
{
    public class PackageVersionEntry
    {
        public string IntegrationName { get; set; }

        public string SampleProjectName { get; set; }

        public string NugetPackageSearchName { get; set; }

        public string MinVersion { get; set; }

        public string MinVersionNetCore { get; set; } = string.Empty;

        public string MaxVersionExclusive { get; set; }

        public string SampleTargetFramework { get; set; }
    }
}
