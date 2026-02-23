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
        .Executes(() =>
        {
            EnsureExistingDirectory(TestDumpsDirectory);

            var category = SmokeTestCategory!.Value;
            var smokeTest = SmokeTests.SmokeTestBuilder.GetScenario(category, SmokeTestScenario);

            Logger.Information("Building test image for {SmokeTestName}...", smokeTest.ShortName);
            SmokeTests.SmokeTestBuilder.BuildImage(category, smokeTest, TracerDirectory);
            Logger.Information("Successfully built image {DockerTag}", smokeTest.DockerTag);
        });
}
