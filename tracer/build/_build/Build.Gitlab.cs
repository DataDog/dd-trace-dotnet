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
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using Target = Nuke.Common.Target;

partial class Build
{
    // Only change temporarily for testing
    bool UseTestPfxCertificate = false;

    Target SignDlls => _ => _
       .Description("Sign the dlls produced by building the Tracer, Profiler, and Monitoring home directory")
       .Unlisted()
       .Requires(() => IsWin)
       .After(BuildTracerHome, BuildProfilerHome, BuildNativeLoader)
       .Before(PackNuGet, BuildMsi, ZipMonitoringHome)
       .Executes(async () =>
        {
            // also sign the NuGet package "bin" folder, as they are what gets packed in the NuGet
            var dllsInBin = ProjectsToPack
                           .Select(project => project.Directory)
                           .SelectMany(projectDir => projectDir.GlobFiles("**/bin/**/Datadog*.dll"));
            var homeDlls = MonitoringHomeDirectory.GlobFiles("**/Datadog*.dll");

            var dlls = homeDlls.Concat(dllsInBin);
            await SignFiles(dlls);
        });

    Target SignMsiAndNupkg => _ => _
       .Description("Sign the nupkg and msi files produced by packaging the Tracer home directory")
       .Unlisted()
       .Requires(() => IsWin)
       .After(PackageTracerHome)
       .Executes(async () =>
        {
            var files = ArtifactsDirectory.GlobFiles("**/*.msi")
                                         .Concat(ArtifactsDirectory.GlobFiles("**/*.nupkg"));
            await SignFiles(files);
        });

    async Task SignFiles(IEnumerable<AbsolutePath> filesToSign)
    {
        // To create a pfx certificate for local testing, use powershell and run:
        // $outputLocation = "test_cert.pfx"
        // $cert = New-SelfSignedCertificate -DnsName sample.contoso.com -Type CodeSigning -CertStoreLocation Cert:\CurrentUser\My
        // $CertPassword = ConvertTo-SecureString -String "Passw0rd" -Force –AsPlainText
        // Export-PfxCertificate -Cert "cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $outputLocation -Password $CertPassword

        var tempFileName = Path.GetTempFileName();
        const string timestampServer = "http://timestamp.digicert.com/";

        try
        {
            var (certPath, certPassword) = UseTestPfxCertificate
                                               ? (@"test_cert.pfx", "Passw0rd")
                                               : await GetSigningMaterial(tempFileName);

            Logger.Info("Signing material retrieved");

            var binaries = filesToSign
               .Where(x => !x.ToString().EndsWith(".nupkg"))
                .ToList();

            if (binaries.Any())
            {
                Logger.Info("Signing binaries...");
                binaries.ForEach(file => SignBinary(certPath, certPassword, file));
                Logger.Info("Binary signing complete");
            }

            var nupkgs = filesToSign
                        .Where(x => x.ToString().EndsWith(".nupkg"))
                        .ToList();

            if (nupkgs.Any())
            {
                Logger.Info("Signing NuGet packages...");
                nupkgs.ForEach(file => SignNuGet(certPath, certPassword, file));
                Logger.Info("NuGet signing complete");
            }
        }
        finally
        {
            File.Delete(tempFileName);
        }

        return;

        void SignBinary(string certPath, string certPassword, AbsolutePath binaryPath)
        {
            Logger.Info($"Signing {binaryPath}");

            SignToolTasks.SignTool(
                x => x
                    .SetFiles(binaryPath)
                    .SetFile(certPath)
                    .SetPassword(certPassword)
                    .SetTimestampServerUrl(timestampServer)
            );
        }

        void SignNuGet(string certPath, string certPassword, AbsolutePath binaryPath)
        {
            Logger.Info($"Signing {binaryPath}");

            // nuke doesn't expose the sign tool
            try
            {
                NuGetTasks.NuGet(
                    $"sign \"{binaryPath}\"" +
                    $" -CertificatePath {certPath}" +
                    $" -CertificatePassword {certPassword}" +
                    $" -Timestamper {timestampServer} -NonInteractive",
                    logOutput: false,
                    logInvocation: false,
                    logTimestamp: false); // don't print to std out/err
            }
            catch (Exception)
            {
                // Exception doesn't say anything useful generally and don't want to expose it if it does
                // so don't log it
                Logger.Error($"Failed to sign nuget package '{binaryPath}");
            }
        }

        async Task<(string CertificateFilePath, string Password)> GetSigningMaterial(string keyFile)
        {
            // Get the signing keys from SSM
            var pfxB64EncodedPart1 = await GetFileValueFromSsmUsingAmazonSdk("keygen.dd_win_agent_codesign.pfx_b64_0");
            var pfxB64EncodedPart2 = await GetFileValueFromSsmUsingAmazonSdk("keygen.dd_win_agent_codesign.pfx_b64_1");
            var pfxPassword = await GetFileValueFromSsmUsingAmazonSdk("keygen.dd_win_agent_codesign.password");

            var pfxB64Encoded = pfxB64EncodedPart1 + pfxB64EncodedPart2;

            Logger.Info($"Retrieved base64 encoded pfx. Length: {pfxB64Encoded.Length}");
            var pfxB64Decoded = Convert.FromBase64String(pfxB64Encoded);

            Logger.Info($"Writing key material to temporary file {keyFile}");
            File.WriteAllBytes(keyFile, pfxB64Decoded);

            Logger.Info("Verifying key material");
            var file = new X509Certificate2(keyFile, pfxPassword);
            file.Verify();

            return (CertificateFilePath: keyFile, Password: pfxPassword);

            static async Task<string> GetFileValueFromSsmUsingAmazonSdk(string filename)
            {
                // NOTE: set the region here to match the region used when you created
                // the parameter
                var region = Amazon.RegionEndpoint.USEast1;
                var request = new GetParameterRequest()
                {
                    Name = filename,
                    WithDecryption = true
                };

                using (var client = new AmazonSimpleSystemsManagementClient(region))
                {
                    try
                    {
                        var response = await client.GetParameterAsync(request);
                        Logger.Info($"Retrieved {filename} from SSM");
                        return response.Parameter.Value;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error fetching {0} from SSM: {1}", filename, ex);
                        throw;
                    }
                }
            }
        }
    }
}
