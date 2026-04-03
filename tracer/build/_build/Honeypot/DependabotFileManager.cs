// <copyright file="DependabotFileManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Nuke.Common.IO;
using UpdateVendors;

namespace Honeypot
{
    public static class DependabotFileManager
    {
        public static void UpdateVendors(AbsolutePath honeypotProject)
        {
            var fakeRefs = string.Empty;

            foreach (var dependency in VendoredDependency.All)
            {
                fakeRefs += $@"{Environment.NewLine}    <!-- https://www.nuget.org/packages/{dependency.LibraryName}/{dependency.Version} -->";
                fakeRefs += $@"{Environment.NewLine}    <PackageReference Include=""{dependency.LibraryName}"" Version=""{dependency.Version}"" />{Environment.NewLine}";
            }

            var honeypotProjTemplate = GetHoneyPotProjTemplate();
            honeypotProjTemplate = honeypotProjTemplate.Replace("##PACKAGE_REFS##", fakeRefs);

            File.WriteAllText(honeypotProject, honeypotProjTemplate);
        }

        private static string GetHoneyPotProjTemplate()
        {
            var thisAssembly = typeof(UpdateVendorsTool).Assembly;
            var resourceStream = thisAssembly.GetManifestResourceStream("Honeypot.Datadog.Dependabot.Honeypot.template");
            using var reader = new StreamReader(resourceStream);

            return reader.ReadToEnd();
        }
    }
}
