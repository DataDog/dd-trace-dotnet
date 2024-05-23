// <copyright file="UpdateHoneypot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using GeneratePackageVersions;
using Nuke.Common.IO;
using PrepareRelease;
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

        public static Task UpdateIntegrations(AbsolutePath honeypotProject, List<IntegrationMap> distinctIntegrations)
        {
            var fakeRefs = string.Empty;
            
            // have to group by packages so we don't have duplicate package references
            // so reverse the dependencies here
            var integrationsByPackageName = distinctIntegrations
                .SelectMany(integration => integration.Packages.Select(package => (integration, package)))
                .GroupBy(x => x.package)
                .OrderBy(x => x.Key.NugetName);

            foreach (var packageNameGroup in integrationsByPackageName)
            {
                var package = packageNameGroup.Key;
                foreach (var (integration, _) in packageNameGroup.OrderBy(x => x.integration.Name))
                {
                    fakeRefs += $@"{Environment.NewLine}    <!-- Integration: {integration.Name} -->";
                    fakeRefs += $@"{Environment.NewLine}    <!--    Assembly: {integration.AssemblyName} -->";
                }

                fakeRefs += $@"{Environment.NewLine}    <!-- Latest package https://www.nuget.org/packages/{package.NugetName}/{package.LatestVersion} -->";
                fakeRefs += $@"{Environment.NewLine}    <PackageReference Include=""{package.NugetName}"" Version=""{package.LatestTestedVersion ?? package.LatestSupportedVersion}"" />{Environment.NewLine}";
            }

            var honeypotProjTemplate = GetHoneyPotProjTemplate();
            honeypotProjTemplate = honeypotProjTemplate.Replace("##PACKAGE_REFS##", fakeRefs);

            return File.WriteAllTextAsync(honeypotProject, honeypotProjTemplate);
        }

        public static List<(string NugetName, Version LatestTestedVersion)> GetCurrentlyTestedVersions(AbsolutePath honeypotProject)
            => XElement.Load(honeypotProject)
                       .Descendants("PackageReference")
                       .Select(x => ((string)x.Attribute("Include"), new Version(((string)x.Attribute("Version"))!)))
                       .Distinct()
                       .ToList();

        public static async Task<List<IntegrationMap>> BuildDistinctIntegrationMaps(List<InstrumentedAssembly> targets, List<PackageVersionGenerator.TestedPackage> testedVersions)
        {
            var distinctIntegrations = new List<IntegrationMap>();

            foreach (var tg in targets.GroupBy(t => t.TargetAssembly))
            {
                var maxVersionTarget =
                    tg
                        .OrderByDescending(a => a.TargetMaximumMajor)
                        .ThenByDescending(a => a.TargetMaximumMinor)
                        .ThenByDescending(a => a.TargetMaximumPatch)
                        .First();
                var minVersionTarget =
                    tg
                        .OrderBy(a => a.TargetMinimumMajor)
                        .ThenBy(a => a.TargetMinimumMinor)
                        .ThenBy(a => a.TargetMinimumPatch)
                        .First();
                var minSupportedVersion =
                    new Version(
                        minVersionTarget.TargetMinimumMajor,
                        minVersionTarget.TargetMinimumMinor,
                        minVersionTarget.TargetMinimumPatch);
                var maxSupportedVersion =
                    new Version(
                        maxVersionTarget.TargetMaximumMajor,
                        maxVersionTarget.TargetMaximumMinor,
                        maxVersionTarget.TargetMaximumPatch);

                distinctIntegrations.Add(
                    await IntegrationMap.Create(
                        name: tg.Key,
                        integrationId: maxVersionTarget.IntegrationName,
                        assemblyName: maxVersionTarget.TargetAssembly,
                        minSupportedVersion,
                        maxSupportedVersion,
                        testedVersions));
            }

            return distinctIntegrations;
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
