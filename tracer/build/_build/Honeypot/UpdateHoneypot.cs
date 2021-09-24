// <copyright file="UpdateHoneypot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common.IO;
using PrepareRelease;
using UpdateVendors;

namespace Honeypot
{
    public static class UpdateHoneypot
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

        public static async Task UpdateIntegrations(AbsolutePath honeypotProject, IntegrationGroups integrations)
        {
            var fakeRefs = string.Empty;

            var targets = integrations.CallTarget;

            var distinctIntegrations = new List<IntegrationMap>();

            foreach (var tg in targets.GroupBy(t => t.TargetAssembly))
            {
                var maxVersionTarget =
                    tg
                      .OrderByDescending(a => a.TargetMaximumMajor)
                      .ThenByDescending(a => a.TargetMaximumMinor)
                      .ThenByDescending(a => a.TargetMaximumPatch)
                      .First();
                var maxVersion =
                    new Version(
                        maxVersionTarget.TargetMaximumMajor,
                        maxVersionTarget.TargetMaximumMinor,
                        maxVersionTarget.TargetMaximumPatch);
                distinctIntegrations.Add(
                    await IntegrationMap.Create(
                        name: tg.Key,
                        assemblyName: maxVersionTarget.TargetAssembly,
                        maxVersion));
            }

            foreach (var integration in distinctIntegrations)
            {
                foreach (var package in integration.Packages)
                {
                    fakeRefs += $@"{Environment.NewLine}    <!-- Integration: {integration.Name} -->";
                    fakeRefs += $@"{Environment.NewLine}    <!-- Assembly: {integration.AssemblyName} -->";
                    fakeRefs += $@"{Environment.NewLine}    <!-- Latest package https://www.nuget.org/packages/{package.NugetName}/{package.LatestNuget} -->";
                    fakeRefs += $@"{Environment.NewLine}    <PackageReference Include=""{package.NugetName}"" Version=""{package.LatestSupportedNuget}"" />{Environment.NewLine}";
                }
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
