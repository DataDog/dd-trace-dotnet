// <copyright file="CustomTestFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;

namespace Datadog.Trace.TestHelpers
{
    internal class CustomTestFramework
    {
        internal static string GetProfilerTargetFolder()
        {
            var tracerHome = EnvironmentHelper.GetTracerHomePath();
            var targetFrameworkDirectory = EnvironmentTools.GetTracerTargetFrameworkDirectory();

            var finalDirectory = Path.Combine(tracerHome, targetFrameworkDirectory);

            if (Directory.Exists(finalDirectory))
            {
                return finalDirectory;
            }

            return null;
        }
    }
}
