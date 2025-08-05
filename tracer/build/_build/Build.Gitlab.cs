using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.PowerShell;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using Target = Nuke.Common.Target;
using Logger = Serilog.Log;

partial class Build
{
    Target DownloadWinSsiTelemetryForwarder => _ => _
       .Description("Downloads the telemetry forwarder executable used by SSI ")
       .Unlisted()
       .Requires(() => IsWin)
       .Before(SignDlls)
       .Executes(async () =>
        {
            // Download the forwarder from Azure for now.
            // We will likely change this in the future, but it'll do for now.
            const string url = "https://apmdotnetci.blob.core.windows.net/apm-datadog-win-ssi-telemetry-forwarder/telemetry_forwarder.exe";
            const string expectedHash = "3A207302683E2BE8CB3D3A6AFF4F40721B7C85763A5A9B8F441D6BFD1B419F02D3EA8155634CEB3A09473F4745CE49679A55095F0EAD91F0582C6CB08C725782";

            var tempFile = await TryDownloadForwarder();
            var actualHash = GetSha512Hash(tempFile);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                throw new Exception($"Downloaded file did not have expected hash. Expected hash {expectedHash}, actual hash {actualHash}");
            }

            Logger.Information("Hash verified: '{Hash}'", expectedHash);

            // Move to expected location
            var output = ArtifactsDirectory / "telemetry_forwarder.exe";
            FileSystemTasks.CopyFile(tempFile, output, FileExistsPolicy.Overwrite);

            static async Task<string> TryDownloadForwarder()
            {
                using var client = new HttpClient();
                var attemptsRemaining = 3;
                var defaultdelay = TimeSpan.FromSeconds(2);

                while (attemptsRemaining > 0)
                {
                    var retryDelay = defaultdelay;
                    try
                    {
                        Logger.Information("Downloading from {Url}", url);
                        using var response = await client.GetAsync(url);
                        var outputPath = Path.GetTempFileName();

                        if (response.IsSuccessStatusCode)
                        {
                            Logger.Information("Saving file to {Path}", outputPath);
                            await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                            await response.Content.CopyToAsync(fs);
                            return outputPath;
                        }

                        Logger.Warning("Failed to download telemetry forwarder, {StatusCode}: {Body}", response.StatusCode, await response.Content.ReadAsStringAsync());

                        if (response.StatusCode == HttpStatusCode.TooManyRequests
                            && response.Headers.TryGetValues("Retry-After", out var values)
                            && values.FirstOrDefault() is {} retryAfter)
                            {
                                if (int.TryParse(retryAfter, out var seconds))
                                {
                                    retryDelay = TimeSpan.FromSeconds(seconds);
                                }
                                else if (DateTimeOffset.TryParse(retryAfter, out var retryDate))
                                {
                                    var delta = retryDate - DateTimeOffset.UtcNow;
                                    retryDelay = delta > TimeSpan.Zero ? delta : retryDelay;
                                }
                            }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Error downloading telemetry forwarder {Url}", url);
                    }

                    attemptsRemaining--;
                    if (attemptsRemaining > 0)
                    {
                        Logger.Debug("Waiting {RetryDelayTotalSeconds} seconds before retry...", retryDelay.TotalSeconds);
                        await Task.Delay(retryDelay);
                    }
                }

                throw new Exception("Failed to download telemetry forwarder");
            }

            static string GetSha512Hash(string filePath)
            {
                using var sha512 = SHA512.Create();
                using var stream = File.OpenRead(filePath);

                var hashBytes = sha512.ComputeHash(stream);
                return Convert.ToHexString(hashBytes);
            }
        });

    Target SignDlls => _ => _
       .Description("Sign the dlls produced by building the Tracer, Profiler, and Monitoring home directory, as well as the dd-dotnet exes")
       .Unlisted()
       .Requires(() => IsWin)
       .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader,  CreateTrimmingFile, BuildDdDotnet, CopyDdDotnet)
       .Before(PackNuGet, BuildMsi, ZipMonitoringHome)
       .Executes(() =>
        {
            // also sign the NuGet package "bin" folder, as they are what gets packed in the NuGet
            var dllsInBin = ProjectsToPack
                           .Select(project => project.Directory)
                           .SelectMany(projectDir => projectDir.GlobFiles("**/bin/**/Datadog*.dll"));
            var homeDlls = MonitoringHomeDirectory.GlobFiles("**/Datadog*.dll");
            var waf = MonitoringHomeDirectory.GlobFiles("**/ddwaf.dll");

            var ddDotnet = MonitoringHomeDirectory.GlobFiles("**/*.exe")
                                                  .Concat(ArtifactsDirectory.GlobFiles("**/*.exe"))
                                                  .Concat(MonitoringHomeDirectory.GlobFiles("**/dd-dotnet"))
                                                  .Concat(ArtifactsDirectory.GlobFiles("**/dd-dotnet"));
            var dlls = homeDlls.Concat(dllsInBin).Concat(waf).Concat(ddDotnet);
            SignFiles(dlls.ToList());
        });

    Target SignMsi => _ => _
       .Description("Sign the msi files produced by packaging the Tracer home directory")
       .Unlisted()
       .Requires(() => IsWin)
       .After(PackageTracerHome)
       .Executes(() =>
        {
            // We don't currently sign the NuGet packages because that would mean
            // _all_ NuGet packages uploaded under the datadog owner would need to be signed.
            // While that would be the best option, it requires everyone to switch across at the same time

            var files = ArtifactsDirectory.GlobFiles("**/*.msi");
            SignFiles(files);
        });

    void SignFiles(IReadOnlyCollection<AbsolutePath> filesToSign)
    {
        Logger.Information("Signing {Count} binaries...", filesToSign.Count);
        filesToSign.ForEach(file => SignBinary(file));
        Logger.Information("Binary signing complete");

        void SignBinary(AbsolutePath binaryPath)
        {
            Logger.Information("Signing {BinaryPath}", binaryPath);

            var signProcess = ProcessTasks.StartProcess(
                    "dd-wcs",
                    $"sign {binaryPath}",
                    logOutput: false,
                    logInvocation: false);
            signProcess.WaitForExit();
            if (signProcess.ExitCode == 0)
            {
                PowerShellTasks.PowerShell($"Get-AuthenticodeSignature {binaryPath}");
            }
            else
            {
                throw new Exception($"Error signing {binaryPath}");
            }
        }
    }
}
