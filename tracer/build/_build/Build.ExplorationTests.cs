using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public enum MonitoringType
{
    Debugger, Profiler
}

public enum ExplorationTestName
{
    eShopOnWeb, protobuf, cake, swashbuckle, paket, ilspy
}

class ExplorationTestDescription
{
    public string GitRepositoryUrl { get; set; }
    public string GitRepositoryTag { get; set; }
    public string Name { get; set; }
    public string PathToUnitTestProject { get; set; }
    public string[] TestsToIgnore { get; set; }

    public TargetFramework[] SupportedFrameworks { get; set; }

    public bool IsFrameworkSupported(TargetFramework targetFramework)
    {
        return SupportedFrameworks.Any(framework => framework.Equals(targetFramework));
    }

    public static ExplorationTestDescription[] GetAllExplorationTestDescriptions()
    {
        return Enum.GetValues<ExplorationTestName>()
                   .Select(GetExplorationTestDescription)
                   .ToArray()
            ;
    }

    public static ExplorationTestDescription GetExplorationTestDescription(ExplorationTestName name)
    {
        var description = name switch
        {
            ExplorationTestName.eShopOnWeb => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/dotnet-architecture/eShopOnWeb.git",
                Name = "eShopOnWeb",
                GitRepositoryTag = "netcore2.1",
                PathToUnitTestProject = "tests/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1 }
            },
            ExplorationTestName.protobuf => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/protocolbuffers/protobuf.git",
                Name = "protobuf",
                GitRepositoryTag = "v3.19.1",
                PathToUnitTestProject = "csharp/src/Google.Protobuf.Test",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1, TargetFramework.NET5_0, }
            },
            ExplorationTestName.cake => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/cake-build/cake.git",
                Name = "cake",
                GitRepositoryTag = "v1.3.0",
                PathToUnitTestProject = "src/Cake.Common.Tests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0 }
            },
            ExplorationTestName.swashbuckle => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore.git",
                Name = "Swashbuckle.AspNetCore",
                GitRepositoryTag = "v6.2.3",
                PathToUnitTestProject = "test/Swashbuckle.AspNetCore.SwaggerGen.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 }
            },
            ExplorationTestName.paket => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/fsprojects/Paket.git",
                Name = "Paket",
                GitRepositoryTag = "6.2.1",
                PathToUnitTestProject = "tests/Paket.Tests",
                TestsToIgnore = new[] { "Loading assembly metadata works" },
                SupportedFrameworks = new[] { TargetFramework.NET461, TargetFramework.NETCOREAPP3_1 }
            },
            ExplorationTestName.ilspy => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/icsharpcode/ILSpy.git",
                Name = "ILSpy",
                GitRepositoryTag = "6.2.1",
                PathToUnitTestProject = "ICSharpCode.Decompiler.Tests",
                TestsToIgnore = new[] { "ICSharpCode.Decompiler.Tests", "UseMc", "_net45", "ImplicitConversions", "ExplicitConversions", "ICSharpCode_Decompiler", "NewtonsoftJson_pcl_debug", "NRefactory_CSharp", "Random_TestCase_1" },
                SupportedFrameworks = new[] { TargetFramework.NET461, TargetFramework.NETCOREAPP3_1 }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

        return description;
    }
}

partial class Build
{
    AbsolutePath ExplorationTestsDirectory => RootDirectory / "exploration-tests";

    [Parameter("Indicates name of exploration test to run. If not specified, will run all tests sequentially.")]
    readonly ExplorationTestName? ExplorationTestName;

    [Parameter("Indicates whether exploration tests should skip repository cloning. Useful for local development. Default false.")]
    readonly bool ExplorationSkipClone;

    [Parameter("Indicates whether exploration tests should run on latest repository commit. Useful if you want to update tested repositories to the latest tags. Default false.", 
               List = false)]
    readonly bool ExplorationCloneLatest;

    Target RunExplorationTests_Debugger
        => _ => _
               .Description("Run exploration tests for debugger.")
               .After(Clean)
               .Requires(() => ExplorationTestName)
               .Executes(() =>
                {
                    PrepareMonitoringEnvironment_Debugger();
                    GitCloneAndRunUnitTests(MonitoringType.Debugger);
                    RunExplorationTestAssertions_Debugger();
                })
        ;

    void PrepareMonitoringEnvironment_Debugger()
    {
        Logger.Info($"Prepare environment variables for profiler.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_Debugger()
    {
        Logger.Info($"Running assertions tests for debugger.");
        //TODO TBD
    }

    Target RunExplorationTests_Profiler
        => _ => _
               .Description("Run exploration tests for profiler.")
               .After(Clean)
               .Executes(() =>
                {
                    PrepareMonitoringEnvironment_Profiler();
                    GitCloneAndRunUnitTests(MonitoringType.Profiler);
                    RunExplorationTestAssertions_Profiler();
                })
        ;


    void PrepareMonitoringEnvironment_Profiler()
    {
        Logger.Info($"Prepare environment variables for profiler.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_Profiler()
    {
        Logger.Info($"Running assertions tests for profiler.");
        //TODO TBD
    }

    void GitCloneAndRunUnitTests(MonitoringType monitoringType)
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Info($"Provided exploration test name is {ExplorationTestName}.");
            GitCloneAndRunUnitTest(ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value), monitoringType);
        }
        else
        {
            Logger.Info($"Exploration test name is not provided. Running all.");

            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                GitCloneAndRunUnitTest(testDescription, monitoringType);
            }
        }
    }

    void GitCloneAndRunUnitTest(ExplorationTestDescription testDescription, MonitoringType monitoringType)
    {
        Logger.Info($"Running exploration test {testDescription.Name}.");

        if (Framework != null && !testDescription.IsFrameworkSupported(Framework))
        {
            Logger.Warn($"This framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
            return;
        }

        var projectPath = GiClone(testDescription);
        DotNetBuild(
            x => x
                .SetProjectFile(projectPath)
                .SetConfiguration(BuildConfiguration)
                .When(Framework != null, settings => settings.SetFramework(Framework))
        );

        DotNetTest(
            x =>
            {
                x = x
                   .SetProjectFile(projectPath)
                   .EnableNoRestore()
                   .EnableNoBuild()
                   .SetConfiguration(BuildConfiguration)
                   .When(Framework != null, settings => settings.SetFramework(Framework))
                   .SetIgnoreFilter(testDescription.TestsToIgnore)
                   .WithMemoryDumpAfter(1)
                    ;

                x = monitoringType switch
                {
                    MonitoringType.Debugger => x.SetDebuggerEnvironmentVariables(TracerHomeDirectory),
                    MonitoringType.Profiler => x.SetProfilerEnvironmentVariables(TracerHomeDirectory),
                    _ => throw new ArgumentOutOfRangeException(nameof(monitoringType), monitoringType, null)
                };

                return x;
            });
    }

    string GiClone(ExplorationTestDescription testDescription)
    {
        if (!ExplorationSkipClone)
        {
            var cloneCommand = ExplorationCloneLatest
                                   ? $"clone {testDescription.GitRepositoryUrl} {ExplorationTestsDirectory}/{testDescription.Name}"
                                   : $"clone -b {testDescription.GitRepositoryTag} {testDescription.GitRepositoryUrl} {ExplorationTestsDirectory}/{testDescription.Name}";


            GitTasks.Git(cloneCommand);
        }

        var projectPath = $"{ExplorationTestsDirectory}/{testDescription.Name}/{testDescription.PathToUnitTestProject}";
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Test path '{projectPath}' doesn't exist.");
        }

        return projectPath;
    }
}
