// <copyright file="CombinatorialParams.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    internal static class CombinatorialParams
    {
        /// <summary>
        /// Special attribute that will provide Xunit with the values of ["disabled", "service", "full"] for the DBM propagation modes.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        public class DbmPropagationModesDataAttribute : CombinatorialValuesAttribute
        {
            public DbmPropagationModesDataAttribute()
                : base(GetDbmPropagationModes())
            {
            }

            /// <summary>
            /// Gets valid DBM propagation modes to be used in tests.
            /// <para>Note that not all integrations support DBM propagation.</para>
            /// </summary>
            /// <returns>3 options: ["disabled", "service", "full"]</returns>
            public static string[] GetDbmPropagationModes()
            {
                return ["disabled", "service", "full"];
            }
        }

        /// <summary>
        /// Special attribute that will provide Xunit with the values of ["v0", "v1"] for the metadata schema version.
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        public class MetadataSchemaVersionDataAttribute : CombinatorialValuesAttribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MetadataSchemaVersionDataAttribute"/> class.
            /// </summary>
            /// <param name="values">The values to pass to this parameter.</param>
            public MetadataSchemaVersionDataAttribute()
                : base(["v0", "v1", "otel"])
            {
            }
        }

        /// <summary>
        /// Special attribute that will provide Xunit with package versions. Supports globs and multiple "dots"! (e.g. "1.0.*", "1.0", "1.2.3.4").
        /// <para>Example Usage (note min/max both optional):</para>
        /// <para>
        /// <c>[PackageVersionData(nameof(PackageVersions.MicrosoftDataSqlClient))]</c>
        /// </para>
        /// <para>
        /// <c>[PackageVersionData(nameof(PackageVersions.MicrosoftDataSqlClient), maxInclusive: "2.0.*")]</c>
        /// </para>
        /// <para>
        /// <c>[PackageVersionData(nameof(PackageVersions.MicrosoftDataSqlClient), minInclusive: "1.0.0", maxInclusive: "2.0.0")]</c>
        /// </para>
        /// </summary>
        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        public class PackageVersionDataAttribute : CombinatorialValuesAttribute
        {
            public PackageVersionDataAttribute(string packageName)
                : base(GetFilteredVersions(packageName, string.Empty, string.Empty))
            {
            }

            public PackageVersionDataAttribute(string packageName, string? minInclusive = null, string? maxInclusive = null)
                : base(GetFilteredVersions(packageName, minInclusive ?? string.Empty, maxInclusive ?? string.Empty))
            {
            }

            private static object?[] GetFilteredVersions(string packageName, string minVersion, string maxVersion)
            {
                var property = typeof(PackageVersions).GetProperty(packageName)
                               ?? throw new ArgumentException($"Package name '{packageName}' not found in PackageVersions.g.cs class.");

                if (property.GetValue(null) is not IEnumerable<object[]> packageVersions)
                {
                    throw new InvalidOperationException($"Package name '{packageName}' did not return a valid IEnumerable<object[]>.");
                }

                return [.. FilterVersions(packageVersions, minVersion, maxVersion).Select(p => p[0])];
            }

            private static IEnumerable<object[]> FilterVersions(IEnumerable<object[]> packageVersions, string minVersion, string maxVersion)
            {
                if (string.IsNullOrEmpty(minVersion) && string.IsNullOrEmpty(maxVersion))
                {
                    return packageVersions;
                }

                // minVersion/maxVersion can contain "*" to indicate any version, we just convert them to a large number
                // granted a glob for a inclusive min version doesn't actually make sense, but here to be consistent
                if (!string.IsNullOrEmpty(minVersion) && minVersion.Contains('*'))
                {
                    minVersion = ConvertGlobToInt(minVersion);
                }

                if (!string.IsNullOrEmpty(maxVersion) && maxVersion.Contains('*'))
                {
                    maxVersion = ConvertGlobToInt(maxVersion);
                }

                _ = Version.TryParse(minVersion, out var min);
                _ = Version.TryParse(maxVersion, out var max);

                return packageVersions.Where(p =>
                {
                    if (p[0] is not string versionString)
                    {
                        return false;
                    }

                    if (string.IsNullOrEmpty(versionString))
                    {
                        // if the version is empty, we assume it satisfies the constraint
                        // this handles the default samples case where we pass a empty string
                        return true;
                    }

                    if (!Version.TryParse(versionString, out var currentPackageVersion))
                    {
                        return false;
                    }

                    // if no min/max provided it always satisfies that constraint
                    // otherwise we check if the current version is within the range

                    var satisfiesMin = string.IsNullOrEmpty(minVersion) || currentPackageVersion >= min;
                    var satisfiesMax = string.IsNullOrEmpty(maxVersion) || currentPackageVersion <= max;

                    return satisfiesMin && satisfiesMax;
                });
            }

            private static string ConvertGlobToInt(string versionString)
            {
                var split = versionString.Split('.');

                foreach (var i in Enumerable.Range(0, split.Length))
                {
                    if (split[i] == "*")
                    {
                        split[i] = "9999";
                    }
                }

                return string.Join(".", split);
            }
        }
    }
}
