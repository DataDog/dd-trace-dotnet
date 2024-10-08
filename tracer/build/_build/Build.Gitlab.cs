using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    Target SignDlls => _ => _
       .Description("Sign the dlls produced by building the Tracer, Profiler, and Monitoring home directory, as well as the dd-dotnet exes")
       .Unlisted()
       .Requires(() => IsWin)
       .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader,  CreateRootDescriptorsFile, BuildDdDotnet, CopyDdDotnet)
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
