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
            const string url = "https://apmdotnetci.blob.core.windows.net/apm-datadog-win-ssi-telemetry-forwarder/c83ee9ad2f93c7314779051662e2e00086a213e0/telemetry_forwarder.exe";
            const string expectedHash = "0B192C1901C670FC9A55464AFDF39774AB7CD0D667ECFB37BC22C27184B49C37D4658383E021F792A2F0C7024E1091F35C3CAD046EC68871FAEEE3C98A40163A";

            var tempFile = await DownloadFile(url);
            var actualHash = GetSha512Hash(tempFile);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                throw new Exception($"Downloaded file did not have expected hash. Expected hash {expectedHash}, actual hash {actualHash}");
            }

            Logger.Information("Hash verified: '{Hash}'", expectedHash);

            // Move to expected location
            var output = ArtifactsDirectory / "telemetry_forwarder.exe";
            FileSystemTasks.CopyFile(tempFile, output, FileExistsPolicy.Overwrite);
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
        const string validSignature = "59063C826DAA5B628B5CE8A2B32015019F164BF0";

        Logger.Information("Signing {Count} binaries...", filesToSign.Count);
        filesToSign.ForEach(file => SignBinary(file));
        Logger.Information("Binary signing complete");

        void SignBinary(AbsolutePath binaryPath)
        {
            Logger.Information("Signing {BinaryPath}", binaryPath);

            var signProcess = ProcessTasks.StartProcess(
                    "c:/devtools/windows-code-signer.exe",
                    $"sign {binaryPath}",
                    logOutput: false,
                    logInvocation: false);
            signProcess.WaitForExit();

            var output = signProcess.Output.Select(o => o.Text);
            foreach (var line in output)
            {
                Logger.Information("[windows-code-signer] {Line}", line);
            }

            if (signProcess.ExitCode == 0)
            {
                var status = PowerShellTasks.PowerShell(
                    $"(Get-AuthenticodeSignature '{binaryPath}').Status",
                    logOutput: false,
                    logInvocation: false);

                var statusValue = status.Select(o => o.Text).FirstOrDefault(l => !string.IsNullOrEmpty(l))?.Trim();

                if (!string.Equals(statusValue, "Valid", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Signature verification failed for {binaryPath}. Status: {statusValue ?? "Empty"}");
                }

                var print = PowerShellTasks.PowerShell(
                    $"(Get-AuthenticodeSignature '{binaryPath}').SignerCertificate.Thumbprint",
                    logOutput: false,
                    logInvocation: false);

                var printValue = print.Select(o => o.Text).FirstOrDefault(l => !string.IsNullOrEmpty(l))?.Trim();

                if (!string.Equals(printValue, validSignature, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Signature verification failed for {binaryPath}. Signature: {printValue ?? "Empty"}");
                }
                else
                {
                    Logger.Information($"Signing verfication of {binaryPath} succedeed. Signature: {printValue}", binaryPath);
                }
            }
            else
            {
                throw new Exception($"Error signing {binaryPath}");
            }
        }
    }
}
