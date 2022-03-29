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
                var currentDirectory = Directory.GetParent(startDirectory);
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

        public static string GetPlatform()
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

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
            // The conditions looks weird, but it seems like _OR_GREATER is not supported yet in all environments
            // We can trim all the additional conditions when this is fixed
#if NETCOREAPP3_1_OR_GREATER || NETCOREAPP3_1 || NET5_0
            return "netcoreapp3.1";
#elif NETCOREAPP || NETSTANDARD
            return "netstandard2.0";
#elif NET461_OR_GREATER || NET461 || NET47 || NET471 || NET472 || NET48
            return "net461";
#else
#error Unexpected TFM
#endif
        }
    }
}
