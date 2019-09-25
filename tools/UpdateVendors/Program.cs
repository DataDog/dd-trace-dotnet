using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;
using Directory = System.IO.Directory;

namespace UpdateVendors
{
    public class Program
    {
        private static readonly string CurrentDirectory = Environment.CurrentDirectory;
        private static readonly string DownloadDirectory = Path.Combine(CurrentDirectory, "downloads");
        private static string _vendorProjectDirectory = Path.Combine(CurrentDirectory, "downloads");

        public static void Main(string[] args)
        {
            InitializeCleanDirectory(DownloadDirectory);
            var solutionDirectory = EnvironmentHelper.GetSolutionDirectory();
            _vendorProjectDirectory = Path.Combine(solutionDirectory, "src", "vendors", "Datadog.Trace.Vendoring");

            UpdateVendor(
                libraryName: "Serilog",
                masterBranchDownload: "https://github.com/serilog/serilog/archive/master.zip",
                pathToSrc: new[] { "serilog-master", "src", "Serilog" },
                (filePath) =>
                {
                    var extension = Path.GetExtension(filePath);

                    if (extension == "cs")
                    {
                        RewriteFileWithTransform(
                            filePath,
                            content =>
                            {
                                // replace things if you want to
                                return content;
                            });
                    }
                });

            UpdateVendor(
                libraryName: "Serilog.Sinks.File",
                masterBranchDownload: "https://github.com/serilog/serilog-sinks-file/archive/master.zip",
                pathToSrc: new[] { "serilog-sinks-file-master", "src", "Serilog.Sinks.File" },
                (filePath) =>
                {
                    var extension = Path.GetExtension(filePath);

                    if (extension == "cs")
                    {
                        RewriteFileWithTransform(
                            filePath,
                            content =>
                            {
                                // replace things if you want to
                                return content;
                            });
                    }
                });
        }

        private static void UpdateVendor(
            string libraryName,
            string masterBranchDownload,
            string[] pathToSrc,
            Action<string> transform = null)
        {
            Console.WriteLine($"Starting {libraryName} upgrade.");

            var zipLocation = Path.Combine(DownloadDirectory, $"{libraryName}.zip");
            var extractLocation = Path.Combine(DownloadDirectory, $"{libraryName}");
            using (var client = new WebClient())
            {
                client.DownloadFile(masterBranchDownload, zipLocation);
            }

            Console.WriteLine($"Downloaded {libraryName} upgrade.");

            ZipFile.ExtractToDirectory(zipLocation, extractLocation);

            Console.WriteLine($"Unzipped {libraryName} upgrade.");

            var sourceLocation = extractLocation;

            foreach (var pathPart in pathToSrc)
            {
                sourceLocation = Path.Combine(sourceLocation, pathPart);
            }

            var projFile = Path.Combine(sourceLocation, $"{libraryName}.csproj");

            // Rename the proj file to a txt for reference
            File.Copy(projFile, projFile + ".txt");
            File.Delete(projFile);
            Console.WriteLine($"Renamed {libraryName} project file.");

            // Delete the assembly properties
            var assemblyPropertiesFolder = Path.Combine(sourceLocation, @"Properties");
            SafeDeleteDirectory(assemblyPropertiesFolder);
            Console.WriteLine($"Deleted {libraryName} assembly properties file.");

            if (transform != null)
            {
                Console.WriteLine($"Running transforms on files for {libraryName}.");

                foreach (var file in Directory.EnumerateFiles(sourceLocation))
                {
                    transform(file);
                }

                foreach (var directory in Directory.EnumerateDirectories(sourceLocation))
                {
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        transform(file);
                    }
                }

                Console.WriteLine($"Finished transforms on files for {libraryName}.");
            }

            // Move it to the vendors directory
            var vendorFinalPath = Path.Combine(_vendorProjectDirectory, libraryName);
            SafeDeleteDirectory(vendorFinalPath);
            Directory.Move(sourceLocation, vendorFinalPath);
            Console.WriteLine($"Copying source of {libraryName} to vendor project.");

            Console.WriteLine($"Finished {libraryName} upgrade.");
        }

        private static void RewriteFileWithTransform(string filePath, Func<string, string> transform)
        {
            var fileContent = File.ReadAllText(filePath);
            fileContent = transform(fileContent);
            File.WriteAllText(
                filePath,
                fileContent,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

        private static string FullVersionReplace(string text, string split)
        {
            return Regex.Replace(text, "thing", "other thing");
        }
    }
}
