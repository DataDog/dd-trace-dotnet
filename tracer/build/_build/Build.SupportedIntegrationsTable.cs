using System.IO;
using CodeGenerators;
using Nuke.Common;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

partial class Build
{
    AbsolutePath SupportedVersionsJson => TracerDirectory / "build" / "supported_versions.json";
    AbsolutePath SupportedIntegrationsOutputDirectory => TracerDirectory / "build" / "integrations";

    Target GenerateSupportedIntegrationsCsv => _ => _
        .Description("Generates CSV files with supported integrations CSVs")
        .Executes(() =>
        {
            // all information / data is just pulled from the supported_versions.json file and just rendered into CSV files
            if (!File.Exists(SupportedVersionsJson))
            {
                Logger.Error("supported_versions.json not found at {Path}", SupportedVersionsJson);
                throw new FileNotFoundException($"Could not find supported_versions.json at {SupportedVersionsJson}");
            }

            Logger.Information("Generating supported integrations CSV files");

            EnsureExistingDirectory(SupportedIntegrationsOutputDirectory);

            SupportedIntegrationsTableGenerator.GenerateCsvFiles(SupportedVersionsJson, SupportedIntegrationsOutputDirectory);

            // Also copy to artifacts directory if it exists
            if (Directory.Exists(ArtifactsDirectory))
            {
                var netCoreCsv = SupportedIntegrationsOutputDirectory / "supported_integrations_netcore.csv";
                var netFxCsv = SupportedIntegrationsOutputDirectory / "supported_integrations_netfx.csv";
                var allCsv = SupportedIntegrationsOutputDirectory / "supported_integrations.csv";

                if (File.Exists(netCoreCsv))
                {
                    CopyFile(netCoreCsv, ArtifactsDirectory / "supported_integrations_netcore.csv", FileExistsPolicy.Overwrite);
                }

                if (File.Exists(netFxCsv))
                {
                    CopyFile(netFxCsv, ArtifactsDirectory / "supported_integrations_netfx.csv", FileExistsPolicy.Overwrite);
                }

                if (File.Exists(allCsv))
                {
                    CopyFile(allCsv, ArtifactsDirectory / "supported_integrations.csv", FileExistsPolicy.Overwrite);
                }
            }

            Logger.Information("Supported Integrations CSV files generated.");
        });
}
