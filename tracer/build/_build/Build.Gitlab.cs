using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using static Nuke.Common.IO.FileSystemTasks;
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
            // also sign the per-project bin output, since these are what gets packed in the NuGet.
            // Under UseArtifactsOutput (tracer/Directory.Build.props) the bin lives at artifacts/bin/<Project>/<pivot>/.
            var dllsInBin = ProjectsToPack
                           .SelectMany(project => (ArtifactsBinDirectory / project.Name).GlobFiles("**/Datadog*.dll"));
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

    Target SignNuGetPackageContents => _ => _
       .Description("Replaces binaries inside pre-built NuGet packages, with signed versions")
       .Unlisted()
       .Requires(() => IsWin)
       .Executes(() =>
        {
            // Important: we must never sign the _package_ itself (no dotnet nuget sign, no
            // .signature.p7s) - see the comment on SignMsi above for why.
            EnsureExistingDirectory(TemporaryDirectory);
            EnsureExistingDirectory(ArtifactsDirectory / "nuget");

            var packages = NuGetPackagesToSignDirectory.GlobFiles("*.nupkg");
            if (packages.Count == 0)
            {
                throw new Exception($"No .nupkg files found in {NuGetPackagesToSignDirectory}");
            }

            Logger.Information("Found {Count} NuGet package(s) to sign in {Directory}", packages.Count, NuGetPackagesToSignDirectory);

            foreach (var package in packages)
            {
                SignNuGetPackage(package, ArtifactsDirectory / "nuget" / package.Name);
            }

            return;

            static bool ShouldSign(string fileName)
            {
                var extension = Path.GetExtension(fileName);
                if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return fileName.StartsWith("Datadog", StringComparison.OrdinalIgnoreCase)
                        || fileName.Equals("ddwaf.dll", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }

            // Streams `sourcePackage` into `destinationPackage`, entry-by-entry and in the original order.
            // Signing candidates (see ShouldSign) are streamed to a scratch file on disk, signed in place,
            // then streamed back into the destination entry; everything else is copied straight through
            // stream-to-stream. We deliberately avoid extracting the whole package to disk and re-zipping
            // it, as that risks emitting backslash path separators or otherwise mangling the OPC parts
            // (e.g. the "//home/linux-x64/dd-dotnet" Content_Types override for extension-less files).
            void SignNuGetPackage(AbsolutePath sourcePackage, AbsolutePath destinationPackage)
            {
                Logger.Information("Signing contents of {Package}", sourcePackage);

                if (File.Exists(destinationPackage))
                {
                    File.Delete(destinationPackage);
                }

                var signedCount = 0;
                var copiedCount = 0;
                // Reused across entries - processing is strictly sequential, so one scratch file is enough.
                var signingTempFile = TemporaryDirectory / "entry-to-sign";

                using (var sourceStream = File.OpenRead(sourcePackage))
                using (var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    if (sourceArchive.GetEntry(".signature.p7s") is not null)
                    {
                        // We must never sign (or repack) an already-signed package - see the comment in
                        // SignNuGetPackageContents above. If we get here, either something upstream started
                        // signing these packages, or we've been pointed at an already-published package by
                        // mistake - either way, fail loudly rather than silently invalidating the signature.
                        throw new Exception(
                            $"{sourcePackage} already contains a '.signature.p7s' entry (a NuGet package signature). " +
                            "We only sign the binaries _inside_ a package, never the package itself - refusing to repack it.");
                    }

                    using var destinationStream = File.Create(destinationPackage);
                    using var destinationArchive = new ZipArchive(destinationStream, ZipArchiveMode.Create);

                    foreach (var sourceEntry in sourceArchive.Entries)
                    {
                        var destinationEntry = destinationArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                        destinationEntry.LastWriteTime = sourceEntry.LastWriteTime;
                        destinationEntry.ExternalAttributes = sourceEntry.ExternalAttributes;

                        if (ShouldSign(sourceEntry.Name))
                        {
                            using (var entryStream = sourceEntry.Open())
                            using (var tempFileStream = File.Create(signingTempFile))
                            {
                                entryStream.CopyTo(tempFileStream);
                            }

                            SignFiles(new[] { signingTempFile });

                            using var signedStream = File.OpenRead(signingTempFile);
                            using var outputStream = destinationEntry.Open();
                            signedStream.CopyTo(outputStream);

                            signedCount++;
                        }
                        else
                        {
                            using var entryStream = sourceEntry.Open();
                            using var outputStream = destinationEntry.Open();
                            entryStream.CopyTo(outputStream);

                            copiedCount++;
                        }

                        File.Delete(signingTempFile);
                    }
                }

                Logger.Information(
                    "Repacked {Package}: {Signed} newly signed, {Copied} copied unchanged",
                    sourcePackage.Name,
                    signedCount,
                    copiedCount);

                VerifyRepackedNuGetPackage(sourcePackage, destinationPackage);
            }

            // Sanity-checks the output of SignNuGetPackage: the set and order of entries must be exactly
            // the same as the source (we should only ever have changed entry _contents_), and we must not
            // have introduced a package signature. Also guards against silently exceeding nuget.org's
            // package size limit, since Authenticode signatures grow the packed binaries slightly.
            static void VerifyRepackedNuGetPackage(AbsolutePath sourcePackage, AbsolutePath destinationPackage)
            {
                using var sourceArchive = ZipFile.OpenRead(sourcePackage);
                using var destinationArchive = ZipFile.OpenRead(destinationPackage);

                var sourceNames = sourceArchive.Entries.Select(e => e.FullName).ToList();
                var destinationNames = destinationArchive.Entries.Select(e => e.FullName).ToList();
                if (!sourceNames.SequenceEqual(destinationNames))
                {
                    throw new Exception($"Repacking {sourcePackage} changed the set or order of entries in the package");
                }

                if (destinationArchive.GetEntry(".signature.p7s") is not null)
                {
                    throw new Exception($"Repacking {sourcePackage} unexpectedly introduced a '.signature.p7s' entry");
                }

                const long maxNuGetOrgPackageSizeBytes = 250L * 1024 * 1024;
                var destinationSize = new FileInfo(destinationPackage).Length;
                if (destinationSize > maxNuGetOrgPackageSizeBytes)
                {
                    throw new Exception(
                        $"{destinationPackage} is {destinationSize / 1024d / 1024d:F1} MB, which exceeds nuget.org's 250 MB package size limit");
                }
            }

        });

    void SignFiles(IReadOnlyCollection<AbsolutePath> filesToSign)
    {
        // See list of certificates
        // in https://datadoghq.atlassian.net/wiki/spaces/SECENG/pages/3217261499/Certificates+for+Windows+Code+Signing
        var expectedCertificateThumbprints = new []
        {
            "A0FB7BEE153FE31431062731306903B3A5CB1824",
            // TODO remove this one when the new certificate is deployed;
            // see https://github.com/DataDog/windows-code-signing-cert/blob/main/current-certs.toml
            "59063C826DAA5B628B5CE8A2B32015019F164BF0",
        };

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

                if (!expectedCertificateThumbprints.Contains(printValue, StringComparer.OrdinalIgnoreCase))
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
