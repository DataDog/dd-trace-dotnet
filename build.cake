#tool "xunit.runner.console"

var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");

Task("Restore")
  .Does(() =>
{
  NuGetRestore("./src/Datadog.Trace.sln");
});

Task("Clean")
  .Does(() =>
{
  DotNetCoreClean("./src",
    new DotNetCoreCleanSettings()
    {
      Configuration = configuration
    });
});

Task("Build")
  .IsDependentOn("Restore")
  .Does(() =>
{
  DotNetCoreBuild("./src",
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
  var projects = GetFiles("./src/*Tests/*.csproj");
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

Task("TestNet45")
  .IsDependentOn("Build")
  .Does(() =>
{
  var testDlls = GetFiles($"./src/**/bin/{configuration}/*Net45.dll");
  foreach(var testDll in testDlls)
  {
    Information(testDll.FullPath);
    XUnit2(testDll.FullPath);
  }
});

Task("Default")
  .IsDependentOn("TestNet45")
  .IsDependentOn("Test")
  .Does(() =>
{
});

RunTarget(target);