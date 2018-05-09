#tool "xunit.runner.console"
#addin "Cake.Docker"

var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");

Task("Pack")
  .Does(() =>
{
  var settings = new DotNetCorePackSettings
  {
    OutputDirectory = "./artifacts",
    Configuration = configuration
  };
  DotNetCorePack("./src/Datadog.Trace", settings);
  DotNetCorePack("./src/Datadog.Trace.OpenTracing", settings);
});

Task("DockerUp")
  .Does(() =>
{
  DockerComposeUp(
    new DockerComposeUpSettings()
    {
      DetachedMode = true
    });
});

Task("DockerDown")
  .Does(() =>
{
  DockerComposeDown();
});

Task("Restore")
  .Does(() =>
{
  NuGetRestore("./Datadog.Trace.sln");
});

Task("Clean")
  .Does(() =>
{
  DotNetCoreClean(".",
    new DotNetCoreCleanSettings()
    {
      Configuration = configuration
    });
});

Task("Build")
  .IsDependentOn("Restore")
  .Does(() =>
{
  DotNetCoreBuild(".",
    new DotNetCoreBuildSettings()
       {
           Configuration = configuration,
           NoRestore = true
       });

});

Task("Test")
  .IsDependentOn("Build")
  .Does(() =>
{
  var projects = GetFiles("./test/*Tests/*.csproj");
  foreach(var project in projects)
  {
    DotNetCoreTest(
      project.FullPath,
      new DotNetCoreTestSettings()
      {
          Configuration = configuration,
          NoBuild = true
      });
  }
});

Task("Benchmarks")
.Does(() =>
{
  DotNetCoreRun("./src/Benchmarks",
    new ProcessArgumentBuilder(),
    new DotNetCoreRunSettings()
    {
      Configuration = "Release"
    });
});

Task("Default")
  .IsDependentOn("Test")
  .Does(() =>
{
});

RunTarget(target);
