using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public enum MonitoringType
{
    Debugger, Profiler

}

[TypeConverter(typeof(ExplorationTestNameTypeConverter))]
public class ExplorationTestName : Enumeration
{
    public static ExplorationTestName eShopOnWeb = new ExplorationTestName { Value = "eshoponweb" };
    public static ExplorationTestName protobuf = new ExplorationTestName { Value = "protobuf" };
    public static ExplorationTestName cake = new ExplorationTestName { Value = "cake" };
    public static ExplorationTestName Swashbuckle = new ExplorationTestName { Value = "swashbuckle" };
    public static ExplorationTestName Paket = new ExplorationTestName { Value = "paket" };
    public static ExplorationTestName ILSpy = new ExplorationTestName { Value = "ilspy" };

    public static implicit operator string(ExplorationTestName type)
    {
        return type.Value;
    }

    public class ExplorationTestNameTypeConverter : TypeConverter<ExplorationTestName>
    {
    }
}

public class ExplorationTestDescription
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

    public static ExplorationTestDescription[] GetAllTestDescriptions()
    {
        return typeof(ExplorationTestName)
              .GetFields(ReflectionService.Static)
              .Select(x => x.GetValue(null))
              .Cast<ExplorationTestName>()
              .Select(GetExplorationTestDescription)
              .ToArray();
    }

    public static ExplorationTestDescription GetExplorationTestDescription(ExplorationTestName testName)
    {
        if (testName.Equals(ExplorationTestName.eShopOnWeb))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/dotnet-architecture/eShopOnWeb.git",
                Name = "eShopOnWeb",
                GitRepositoryTag = "netcore2.1",
                PathToUnitTestProject = "tests/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1 }
            };
        }

        if (testName.Equals(ExplorationTestName.protobuf))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/protocolbuffers/protobuf.git",
                Name = "protobuf",
                GitRepositoryTag = "v3.19.1",
                PathToUnitTestProject = "csharp/src/Google.Protobuf.Test",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1, TargetFramework.NET5_0, }
            };
        }

        if (testName.Equals(ExplorationTestName.cake))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/cake-build/cake.git",
                Name = "cake",
                GitRepositoryTag = "v1.3.0",
                PathToUnitTestProject = "src/Cake.Common.Tests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0 }

            };
        }

        if (testName.Equals(ExplorationTestName.Swashbuckle))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore.git",
                Name = "Swashbuckle.AspNetCore",
                GitRepositoryTag = "v6.2.3",
                PathToUnitTestProject = "test/Swashbuckle.AspNetCore.SwaggerGen.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 }
            };
        }

        if (testName.Equals(ExplorationTestName.Paket))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/fsprojects/Paket.git",
                Name = "Paket",
                GitRepositoryTag = "6.2.1",
                PathToUnitTestProject = "tests/Paket.Tests",
                TestsToIgnore = new[] { "Loading assembly metadata works" },
                SupportedFrameworks = new[] { TargetFramework.NET461, TargetFramework.NETCOREAPP3_1 }
            };
        }

        if (testName.Equals(ExplorationTestName.ILSpy))
        {
            return new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/icsharpcode/ILSpy.git",
                Name = "ILSpy",
                GitRepositoryTag = "6.2.1",
                PathToUnitTestProject = "ICSharpCode.Decompiler.Tests",
                TestsToIgnore = new[] { "ICSharpCode.Decompiler.Tests", "UseMc", "_net45", "ImplicitConversions", "ExplicitConversions", "ICSharpCode_Decompiler", "NewtonsoftJson_pcl_debug", "NRefactory_CSharp", "Random_TestCase_1" },
                SupportedFrameworks = new[] { TargetFramework.NET461, TargetFramework.NETCOREAPP3_1 }
            };
        }

        throw new NotImplementedException($"This exploration test '{testName}' is not supported");
    }
}

partial class Build
{
    AbsolutePath ExplorationTestsDirectory => RootDirectory / "exploration-tests";

    [Parameter("Indicates name of exploration test to run. If not specified, will run all tests sequentially.")]
    readonly ExplorationTestName ExplorationTestName;

    [Parameter("Indicates whether exploration tests should skip cloning. Default false.")]
    readonly bool SkipClone;

    [Parameter("Indicates whether build of exploration tests should be skipped. Useful for local development. Default false.")]
    readonly bool SkipBuild;

    [Parameter("Indicates whether exploration tests should run on latest repository commit. Useful if you want to update tested repositories to the latest tags. Default false.")]
    readonly bool CloneLatest;

    Target RunExplorationTests_Debugger
        => _ => _
               .Description("Run exploration tests for debugger.")
               .Unlisted()
               .After(Clean)
               .DependsOn(PrepareMonitoringEnvironment_Debugger)
               .Executes(() =>
               {
                   GitCloneAndRunUnitTests(MonitoringType.Debugger);
               })
               .Triggers(RunExplorationTestAssertions_Debugger)
        ;

    Target PrepareMonitoringEnvironment_Debugger
        => _ => _
           .Executes(() =>
           {
               Logger.Info($"Prepare environment variables for debugger.");
               //TODO TBD
           })
        ;

    Target RunExplorationTestAssertions_Debugger
        => _ => _
               .Description("Run exploration test assertions.")
               .Unlisted()
               .Executes(() =>
               {
                   Logger.Info($"Running assertions tests for debugger.");
                   //TODO TBD
               });

    Target RunExplorationTests_Profiler
        => _ => _
               .Description("Run exploration tests for profiler.")
               .After(Clean)
               .DependsOn(PrepareMonitoringEnvironment_Profiler)
               .Executes(() =>
               {
                   GitCloneAndRunUnitTests(MonitoringType.Profiler);
               })
               .Triggers(RunExplorationTestAssertions_Profiler)
        ;

    Target PrepareMonitoringEnvironment_Profiler
        => _ => _
           .Executes(() =>
           {
               Logger.Info($"Prepare environment variables for profiler.");
               //TODO TBD
           })
        ;

    Target RunExplorationTestAssertions_Profiler
        => _ => _
               .Description("Run exploration test assertions.")
               .Unlisted()
               .Executes(() =>
               {
                   Logger.Info($"Running assertions tests for profiler.");
                   //TODO TBD
               });

    void GitCloneAndRunUnitTests(MonitoringType monitoringType)
    {
        if (ExplorationTestName != null)
        {
            GitCloneAndRunUnitTest(ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName), monitoringType);
        }
        else
        {
            foreach (var testDescription in ExplorationTestDescription.GetAllTestDescriptions())
            {
                GitCloneAndRunUnitTest(testDescription, monitoringType);
            }
        }
    }

    void GitCloneAndRunUnitTest(ExplorationTestDescription testDescription, MonitoringType monitoringType)
    {
        if (Framework != null && !testDescription.IsFrameworkSupported(Framework))
        {
            Logger.Warn($"This framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
            return;
        }

        var projectPath = GiClone(testDescription);
        if (!SkipBuild)
        {
            DotNetBuild(
                x => x
                    .SetProjectFile(projectPath)
                    .SetConfiguration(BuildConfiguration)
                    .When(Framework != null, settings => settings.SetFramework(Framework))
            );
        }

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
        if (!SkipClone)
        {
            var cloneCommand = CloneLatest
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
