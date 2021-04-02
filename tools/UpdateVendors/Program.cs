using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Datadog.Trace.TestHelpers;

namespace UpdateVendors
{
    public class Program
    {
        private static readonly string DownloadDirectory = Path.Combine(Environment.CurrentDirectory, "downloads");
        private static string _vendorProjectDirectory;

        public static void Main()
        {
            InitializeCleanDirectory(DownloadDirectory);
            var solutionDirectory = GetSolutionDirectory();
            _vendorProjectDirectory = Path.Combine(solutionDirectory, "src", "Datadog.Trace", "Vendors");

            var fakeRefs = string.Empty;

            foreach (var dependency in VendoredDependency.All)
            {
                fakeRefs += $@"{Environment.NewLine}    <!-- https://www.nuget.org/packages/{dependency.LibraryName}/{dependency.Version} -->";
                fakeRefs += $@"{Environment.NewLine}    <PackageReference Include=""{dependency.LibraryName}"" Version=""{dependency.Version}"" />{Environment.NewLine}";
                UpdateVendor(dependency);
            }

            var honeypotProjTemplate = GetHoneyPotProjTemplate();
            honeypotProjTemplate = honeypotProjTemplate.Replace("##PACKAGE_REFS##", fakeRefs);
            var projLocation = Path.Combine(EnvironmentHelper.GetSolutionDirectory(), "honeypot", "Datadog.Dependabot.Honeypot.csproj");
            File.WriteAllText(projLocation, honeypotProjTemplate);
        }

        private static string GetHoneyPotProjTemplate()
        {
            var templateName = "Datadog.Dependabot.Honeypot.template";
            var directory = Directory.GetCurrentDirectory();
            string template = null;
            var levelLimit = 4;

            while (template == null && --levelLimit >= 0)
            {
                foreach (var filePath in Directory.EnumerateFiles(directory))
                {
                    if (filePath.Contains(templateName))
                    {
                        template = File.ReadAllText(filePath);
                        break;
                    }
                }

                directory = Directory.GetParent(directory).FullName;
            }

            return template;
        }

        private static void UpdateVendor(VendoredDependency dependency)
        {
            var libraryName = dependency.LibraryName;
            var downloadUrl = dependency.DownloadUrl;
            var pathToSrc = dependency.PathToSrc;

            Console.WriteLine($"Starting {libraryName} upgrade.");

            var zipLocation = Path.Combine(DownloadDirectory, $"{libraryName}.zip");
            var extractLocation = Path.Combine(DownloadDirectory, $"{libraryName}");
            var vendorFinalPath = Path.Combine(_vendorProjectDirectory, libraryName);
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

        private static void InitializeCleanDirectory(string directoryPath)
        {
            SafeDeleteDirectory(directoryPath);
            Directory.CreateDirectory(directoryPath);
        }

        private static void SafeDeleteDirectory(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        private static string GetSolutionDirectory()
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

            return currentDirectory.FullName;
        }
    }
}
