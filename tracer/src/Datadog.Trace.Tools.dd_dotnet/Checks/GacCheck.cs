// <copyright file="GacCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Spectre.Console;
using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet.Checks
{
    internal class GacCheck
    {
        public static bool Run()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            var gacFolder = Environment.ExpandEnvironmentVariables(Path.Combine("%WINDIR%", "Microsoft.NET", "assembly", "GAC_MSIL", "Datadog.Trace"));

            if (!Directory.Exists(gacFolder))
            {
                Utils.WriteError(MissingGac);
                return false;
            }

            bool foundVersions = false;

            foreach (var folder in Directory.GetDirectories(gacFolder))
            {
                if (!File.Exists(Path.Combine(folder, "Datadog.Trace.dll")))
                {
                    continue;
                }

                var name = new DirectoryInfo(folder).Name;

                // Format: v4.0_2.3.0.0__def86d061d0d2eeb
                var match = Regex.Match(name, @"v4.0_(?<version>\d+\.\d+\.\d+\.\d+)__def86d061d0d2eeb");

                var version = match.Success ? match.Groups["version"].Value : name;

                AnsiConsole.WriteLine(GacVersionFormat(version));
                foundVersions = true;
            }

            if (!foundVersions)
            {
                Utils.WriteError(MissingGac);
                return false;
            }

            return true;
        }
    }
}
