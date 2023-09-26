// <copyright file="EnvironmentTools.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// General use utility methods for all tests and tools.
    /// </summary>
    public class EnvironmentTools
    {
        public const string ProfilerClsId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        public const string DotNetFramework = ".NETFramework";
        public const string CoreFramework = ".NETCoreApp";

        private static string _solutionDirectory = null;

        /// <summary>
        /// Find the solution directory from anywhere in the hierarchy.
        /// </summary>
        /// <returns>The solution directory.</returns>
        public static string GetSolutionDirectory()
        {
            if (_solutionDirectory == null)
            {
                var startDirectory = Environment.CurrentDirectory;
                var currentDirectory = new DirectoryInfo(startDirectory);
                const string searchItem = @"Datadog.Trace.sln";

                while (true)
                {
                    var slnFile = currentDirectory.GetFiles(searchItem).SingleOrDefault();

                    if (slnFile != null)
                    {
                        break;
                    }

                    currentDirectory = currentDirectory.Parent;

                    if (currentDirectory == null || !currentDirectory.Exists)
                    {
                        throw new Exception($"Unable to find solution directory from: {startDirectory}");
                    }
                }

                _solutionDirectory = currentDirectory.FullName;
            }

            return _solutionDirectory;
        }

        public static string GetOS()
        {
            return IsWindows()                                       ? "win" :
                   RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux) ? "linux" :
                   RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)   ? "osx" :
                                                                       string.Empty;
        }

        public static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        }

        public static bool IsLinux()
        {
            return RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        }

        public static bool IsOsx()
        {
            return RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
        }

        public static string GetPlatform()
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        public static string GetTestTargetPlatform()
        {
            var requested = Environment.GetEnvironmentVariable("TargetPlatform");
            return string.IsNullOrEmpty(requested) ? GetPlatform() : requested.ToUpperInvariant();
        }

        public static bool IsTestTarget64BitProcess()
            => GetTestTargetPlatform() != "X86";

        public static string GetBuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        public static string GetTracerTargetFrameworkDirectory()
        {
#if NET6_0_OR_GREATER
            return "net6.0";
#elif NETCOREAPP3_1_OR_GREATER
            return "netcoreapp3.1";
#elif NETCOREAPP || NETSTANDARD
            return "netstandard2.0";
#elif NETFRAMEWORK
            return "net461";
#else
#error Unexpected TFM
#endif
        }
    }
}
