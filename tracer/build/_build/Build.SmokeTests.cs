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
}
