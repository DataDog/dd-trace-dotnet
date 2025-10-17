// <copyright file="UpdateVendors.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Nuke.Common.Tooling;
using Logger = Serilog.Log;
using Nuke.Common.IO;

namespace UpdateVendors
{
    public static class UpdateVendorsTool
    {
        public static async Task UpdateVendors(
            AbsolutePath downloadDirectory,
            AbsolutePath vendorDirectory,
            AbsolutePath traceDirectory)
        {
            foreach (var dependency in VendoredDependency.All)
            {
                await UpdateVendor(dependency, downloadDirectory, vendorDirectory);
            }

            // Generate C# from vendored .proto files
            // Dependency: Grpc.Tools 2.72.0
            var zipLocation = Path.Combine(downloadDirectory, "Grpc.Tools.zip");
            var extractLocation = Path.Combine(downloadDirectory, "Grpc.Tools");
            using (var grpcToolsDownloadClient = new HttpClient())
            {
                await using var stream = await grpcToolsDownloadClient.GetStreamAsync("https://www.nuget.org/api/v2/package/Grpc.Tools/2.72.0");
                await using var file = File.Create(zipLocation);
                await stream.CopyToAsync(file);
            }

            ZipFile.ExtractToDirectory(zipLocation, extractLocation);

            try
            {
                var supportedOsArchitectures = string.Join(", ", Directory.EnumerateDirectories(Path.Combine(extractLocation, "tools")).Select(Path.GetFileName));
                var protoDirectory = vendorDirectory / "protos";
                var importsPath = Path.Combine(extractLocation, "build", "native", "include");
                var protocPath = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Path.Combine(extractLocation, "tools", "windows_x86", "protoc.exe"),
                    Architecture.X86 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => Path.Combine(extractLocation, "tools", "linux_x86", "protoc"),
                    Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => Path.Combine(extractLocation, "tools", "windows_x64", "protoc.exe"),
                    Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => Path.Combine(extractLocation, "tools", "linux_x64", "protoc"),
                    Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => Path.Combine(extractLocation, "tools", "linux_arm64", "protoc"),
                    _ => throw new Exception($"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}. Supported architectures: {supportedOsArchitectures}")
                };

                // Iterate over the protoDirectory to get the full name of each .proto file
                var protoFiles = Directory.EnumerateFiles(protoDirectory, "*.proto", SearchOption.AllDirectories)
                                         .Select(Path.GetFullPath)
                                         .ToList();

                var protoFilesString = string.Join(" ", protoFiles.Select(file => $"\"{file}\""));

                Logger.Information($"Generating C# from .proto files using {protocPath}");
                var process = ProcessTasks.StartProcess(
                    protocPath,
                    $"-I {importsPath} --proto_path={protoDirectory} --csharp_out=\"{traceDirectory}\" --csharp_opt=internal_access,file_extension=.g.cs,base_namespace= {protoFilesString}",
                    workingDirectory: protoDirectory,
                    logOutput: true,
                    logInvocation: true);
                process.AssertZeroExitCode();

                Logger.Information($"Updating generated C# files with Datadog.Trace.Vendors namespace");
                var generatedFiles = Directory.EnumerateFiles(traceDirectory / "OpenTelemetry", "*.g.cs", SearchOption.AllDirectories);
                foreach (var file in generatedFiles)
                {
                    var content = File.ReadAllText(file);
                    content = content.Replace("Google.Protobuf", "Datadog.Trace.Vendors.Google.Protobuf");
                    File.WriteAllText(file, content);
                }
            }
            catch (Exception e)
            {
                Logger.Warning($"Unable to generate C# from .proto files: {e}");
            }
        }

        private static async Task UpdateVendor(VendoredDependency dependency, AbsolutePath downloadDirectory, AbsolutePath vendorDirectory)
        {
            var libraryName = dependency.LibraryName;
            var downloadUrl = dependency.DownloadUrl;
            var pathToSrc = dependency.PathToSrc;
            var pathToDestination = dependency.PathToDestination;

            Console.WriteLine($"Starting {libraryName} upgrade.");

            var zipLocation = Path.Combine(downloadDirectory, $"{libraryName}.zip");
            var extractLocation = Path.Combine(downloadDirectory, $"{libraryName}");
            var vendorFinalPath = Path.Combine(pathToDestination.Prepend(vendorDirectory).ToArray());
            var sourceUrlLocation = Path.Combine(vendorFinalPath, "_last_downloaded_source_url.txt");

            // Ensure the url has changed, or don't bother upgrading
            if (File.Exists(sourceUrlLocation))
            {
                var currentSource = File.ReadAllText(sourceUrlLocation);
                if (currentSource.Equals(downloadUrl, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"No updates to be made for {libraryName}.");
                    return;
                }
            }

            using (var repoDownloadClient = new HttpClient())
            {
                await using var stream = await repoDownloadClient.GetStreamAsync(downloadUrl);
                await using var file = File.Create(zipLocation);
                await stream.CopyToAsync(file);
            }

            Console.WriteLine($"Downloaded {libraryName} upgrade.");

            ZipFile.ExtractToDirectory(zipLocation, extractLocation);

            Console.WriteLine($"Unzipped {libraryName} upgrade.");

            var sourceLocation = Path.Combine(pathToSrc.Prepend(extractLocation).ToArray());
            var projFile = Path.Combine(sourceLocation, $"{libraryName}.csproj");

            // Rename the proj file to a txt for reference
            // To vendor non-C# repos, the .csproj file must be optional
            if (dependency.IsNugetPackage)
            {
                File.Copy(projFile, projFile + ".txt");
                File.Delete(projFile);
                Console.WriteLine($"Renamed {libraryName} project file.");
            }

            // Delete the assembly info file
            var assemblyInfo = Path.Combine(sourceLocation, @"Properties", "AssemblyInfo.cs");
            if (File.Exists(assemblyInfo))
            {
                File.Delete(assemblyInfo);
            }

            Console.WriteLine($"Deleted {libraryName} assembly info file.");

            Console.WriteLine($"Running transforms on files for {libraryName}.");

            var files = Directory.GetFiles(
                sourceLocation,
                "*.*",
                SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (ShouldDropFile(dependency, sourceLocation, file))
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
            EnsureParentDirectoryExists(vendorFinalPath);
            Directory.Move(sourceLocation, vendorFinalPath);
            File.WriteAllText(sourceUrlLocation, downloadUrl);
            Console.WriteLine($"Finished {libraryName} upgrade.");
        }

        private static bool ShouldDropFile(VendoredDependency dependency, string basePath, string filePath)
        {
            var normalizedFilePath = filePath.Replace('/', '\\');
            foreach (var relativeFileToDrop in dependency.RelativePathsToExclude)
            {
                var absolutePath = Path.Combine(basePath, relativeFileToDrop).Replace('/', '\\');
                if (normalizedFilePath.Equals(absolutePath, StringComparison.OrdinalIgnoreCase)
                 || (absolutePath.EndsWith('\\') &&
                     normalizedFilePath.StartsWith(absolutePath, StringComparison.OrdinalIgnoreCase)))
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

        private static void EnsureParentDirectoryExists(string directoryPath)
        {
            var parentDirectory = Directory.GetParent(directoryPath).FullName;
            if (!Directory.Exists(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }
        }
    }
}
