using System;
using Nuke.Common;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

partial class Build
{
    [Parameter("The category of the smoke test to run")]
    readonly SmokeTests.SmokeTestCategory? SmokeTestCategory;
    [Parameter("The specific scenario for the given category to run")]
    readonly string SmokeTestScenario;

    Target RunArtifactSmokeTests => _ => _
        .Description("Runs the artifact snapshot/smoke tests")
        .Unlisted()
        .Requires(() => SmokeTestCategory)
        .Requires(() => SmokeTestScenario)
        .Executes(async () =>
        {
            await SmokeTests.SmokeTestRunner.RunSmokeTestAsync(
                SmokeTestCategory!.Value,
                SmokeTestScenario,
                TracerDirectory,
                ArtifactsDirectory,
                BuildDataDirectory,
                Version,
                GetDotnetSdkVersion(RootDirectory));
        });

    Target UpdateSmokeTestImageDigests => _ => _
        .Description("Queries each registry and updates pinned sha256 digests in smoke-test-images.docker-compose.yml, respecting a 2-day cooldown")
        .Unlisted()
        .Executes(async () =>
        {
            var composeFile = TracerDirectory / "build" / "_build" / "SmokeTests" / "smoke-test-images.docker-compose.yml";
            var cooldown = PackageVersionCooldownDays.HasValue
                             ? TimeSpan.FromDays(PackageVersionCooldownDays.Value)
                             : TimeSpan.FromDays(0);
            using var updater = new SmokeTests.SmokeTestImageDigestUpdater(cooldown);
            await updater.UpdateAsync(composeFile);

            if (updater.CooldownReport.HasEntries)
            {
                var reportPath = TemporaryDirectory / "smoke_test_image_cooldown_report.md";
                await updater.CooldownReport.SaveToFile(reportPath);
                Logger.Information("Image digest cooldown report saved to {Path}", reportPath);
            }
        });
}
