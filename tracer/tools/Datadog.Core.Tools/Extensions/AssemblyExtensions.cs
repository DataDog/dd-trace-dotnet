// <copyright file="AssemblyExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.Versioning;

namespace Datadog.Core.Tools.Extensions
{
    public static class AssemblyExtensions
    {
        private const string DotNetFramework = ".NETFramework";
        private const string CoreFramework = ".NETCoreApp";

        public static string GetTargetFrameworkMoniker(this Assembly assembly)
        {
            var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            var parts = targetFramework?.FrameworkName?.Split(',');

            if (parts?.Length > 1)
            {
                var runtime = parts[0];
                var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');

                if (versionParts.Length > 1)
                {
                    var major = int.Parse(versionParts[0]);
                    var minor = int.Parse(versionParts[1]);

                    if (runtime.Equals(CoreFramework))
                    {
                        return $"netcoreapp{major}.{minor}";
                    }

                    if (runtime.Equals(DotNetFramework))
                    {
                        var patch = versionParts.Length > 2 ? versionParts[2] : null;
                        return $"net{major}{minor}{patch}";
                    }
                }
            }

            return "unsupported";
        }
    }
}
