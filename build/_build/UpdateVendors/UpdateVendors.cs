// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Nuke.Common.IO;

namespace UpdateVendors
{
    public static class UpdateVendorsTool
    {
        public static void UpdateHoneypotProject(AbsolutePath honeypotProject)
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

        public static void UpdateVendors(
            AbsolutePath downloadDirectory,
            AbsolutePath vendorDirectory)
        {
            foreach (var dependency in VendoredDependency.All)
            {
                UpdateVendor(dependency, downloadDirectory, vendorDirectory);
            }
        }

        private static string GetHoneyPotProjTemplate()
        {
            var thisAssembly = typeof(UpdateVendorsTool).Assembly;
            var resourceStream = thisAssembly.GetManifestResourceStream("UpdateVendors.Datadog.Dependabot.Honeypot.template");
            using var reader = new StreamReader(resourceStream);

            return reader.ReadToEnd();
        }

        private static void UpdateVendor(VendoredDependency dependency, AbsolutePath downloadDirectory, AbsolutePath vendorDirectory)
        {
            var libraryName = dependency.LibraryName;
            var downloadUrl = dependency.DownloadUrl;
            var pathToSrc = dependency.PathToSrc;

            Console.WriteLine($"Starting {libraryName} upgrade.");

            var zipLocation = Path.Combine(downloadDirectory, $"{libraryName}.zip");
            var extractLocation = Path.Combine(downloadDirectory, $"{libraryName}");
            var vendorFinalPath = Path.Combine(vendorDirectory, libraryName);
            var sourceUrlLocation = Path.Combine(vendorFinalPath, "_last_downloaded_source_url.txt");

            // Ensure the url has changed, or don't bother upgrading
            var currentSource = File.ReadAllText(sourceUrlLocation);
            if (currentSource.Equals(downloadUrl, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"No updates to be made for {libraryName}.");
                return;
            }

            using (var repoDownloadClient = new WebClient())
            {
                repoDownloadClient.DownloadFile(downloadUrl, zipLocation);
            }

            Console.WriteLine($"Downloaded {libraryName} upgrade.");

            ZipFile.ExtractToDirectory(zipLocation, extractLocation);

            Console.WriteLine($"Unzipped {libraryName} upgrade.");

            var sourceLocation = Path.Combine(pathToSrc.Prepend(extractLocation).ToArray());
            var projFile = Path.Combine(sourceLocation, $"{libraryName}.csproj");

            // Rename the proj file to a txt for reference
            File.Copy(projFile, projFile + ".txt");
            File.Delete(projFile);
            Console.WriteLine($"Renamed {libraryName} project file.");

            // Delete the assembly properties
            var assemblyPropertiesFolder = Path.Combine(sourceLocation, @"Properties");
            SafeDeleteDirectory(assemblyPropertiesFolder);
            Console.WriteLine($"Deleted {libraryName} assembly properties file.");

            Console.WriteLine($"Running transforms on files for {libraryName}.");

            var files = Directory.GetFiles(
                sourceLocation,
                "*.*",
                SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (ShouldDropFile(file))
                {
                    File.Delete(file);
                }
                else
                {
                    dependency.Transform(file);
                }
            }

            Console.WriteLine($"Finished transforms on files for {libraryName}.");

            // Move it all to the vendors directory
            Console.WriteLine($"Copying source of {libraryName} to vendor project.");
            SafeDeleteDirectory(vendorFinalPath);
            Directory.Move(sourceLocation, vendorFinalPath);
            File.WriteAllText(sourceUrlLocation, downloadUrl);
            Console.WriteLine($"Finished {libraryName} upgrade.");
        }

        private static bool ShouldDropFile(string filePath)
        {
            var drops = new List<string>()
            {
                // No active exclusions
            };

            foreach (var drop in drops)
            {
                if (filePath.Contains(drop, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SafeDeleteDirectory(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
