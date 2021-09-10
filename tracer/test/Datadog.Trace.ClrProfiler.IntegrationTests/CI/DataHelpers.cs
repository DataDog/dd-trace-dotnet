// <copyright file="DataHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI
{
    public static class DataHelpers
    {
        /// <summary>
        /// Search up for the Ci/Data Directory to avoid copying so much data around
        /// </summary>
        /// <exception cref="DirectoryNotFoundException">Thrown if the CI/Data path can't be found</exception>
        /// <returns>The path to the CI/Data directory</returns>
        public static string GetCiDataDirectory()
        {
            static string BuildPath(string basePath) => Path.Combine(basePath, "CI", "Data");

            var currentDirectory = Environment.CurrentDirectory;

            while (!Directory.Exists(BuildPath(currentDirectory)))
            {
                var parent = Directory.GetParent(currentDirectory);
                if (parent == null)
                {
                    // Walked all the way up
                    throw new DirectoryNotFoundException($"CI/Data path not found: {BuildPath(Environment.CurrentDirectory)}");
                }

                currentDirectory = parent.FullName;
            }

            return BuildPath(currentDirectory);
        }
    }
}
