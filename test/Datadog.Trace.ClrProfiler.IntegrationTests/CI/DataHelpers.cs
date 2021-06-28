// <copyright file="DataHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;

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
            var solutionFolder = TryGetSolutionDirectoryInfo(Environment.CurrentDirectory);
            if (solutionFolder is null)
            {
                throw new DirectoryNotFoundException("Unable to find solution directory to locate CI/Data data");
            }

            var ciDataPath = Path.Combine(solutionFolder.FullName, "test", "Datadog.Trace.ClrProfiler.IntegrationTests", "CI", "Data");

            if (!Directory.Exists(ciDataPath))
            {
                throw new DirectoryNotFoundException($"Unable to find CI/Data at {ciDataPath}");
            }

            return ciDataPath;

            static DirectoryInfo TryGetSolutionDirectoryInfo(string currentPath)
            {
                var directory = new DirectoryInfo(currentPath);
                while (directory is not null && !directory.GetFiles("*.sln").Any())
                {
                    directory = directory.Parent;
                }

                return directory;
            }
        }
    }
}
