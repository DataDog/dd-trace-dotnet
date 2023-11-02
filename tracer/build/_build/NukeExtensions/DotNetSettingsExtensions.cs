using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;

internal static partial class DotNetSettingsExtensions
{
    public static DotNetBuildSettings SetTargetPlatformAnyCPU(this DotNetBuildSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetTestSettings SetTargetPlatformAnyCPU(this DotNetTestSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetPublishSettings SetTargetPlatformAnyCPU(this DotNetPublishSettings settings)
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static T SetTargetPlatformAnyCPU<T>(this T settings)
        where T: MSBuildSettings
        => settings.SetTargetPlatform(MSBuildTargetPlatform.MSIL);

    public static DotNetBuildSettings SetTargetPlatform(this DotNetBuildSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }
    public static DotNetTestSettings SetTargetPlatform(this DotNetTestSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetPublishSettings SetTargetPlatform(this DotNetPublishSettings settings, MSBuildTargetPlatform platform)
    {
        return platform is null
            ? settings
            : settings.SetProperty("Platform", GetTargetPlatform(platform));
    }

    public static DotNetTestSettings SetIsDebugRun(this DotNetTestSettings settings, bool isDebugRun)
        => settings.When(isDebugRun, c => c.SetProcessEnvironmentVariable("DD_TRACE_DEBUG", "1"));

    private static string GetTargetPlatform(MSBuildTargetPlatform platform) =>
        platform == MSBuildTargetPlatform.MSIL ? "AnyCPU" : platform.ToString();

    public static T SetNoWarnDotNetCore3<T>(this T settings)
        where T: ToolSettings
    {
        return settings.SetProcessArgumentConfigurator(
            arg => arg.Add("/nowarn:netsdk1138"));
    }

    public static T SetPlatform<T>(this T settings, MSBuildTargetPlatform platform)
        where T: NuGetRestoreSettings
    {
        return settings.SetProcessArgumentConfigurator(
            arg => arg.Add($"/p:\"Platform={platform}\""));
    }

    public static T SetDDEnvironmentVariables<T>(this T settings, string serviceName)
        where T: ToolSettings
    {
        return settings.SetProcessEnvironmentVariable("DD_SERVICE_NAME", serviceName);
    }

    public static T SetLogsDirectory<T>(this T settings, AbsolutePath logsDirectory)
        where T: ToolSettings
    {
        return settings.SetProcessEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", logsDirectory);
    }

    public static T SetProcessEnvironmentVariables<T>(this T settings, IEnumerable<KeyValuePair<string, string>> variables)
        where T: ToolSettings
    {
        foreach (var keyValuePair in variables)
        {
            settings = settings.SetProcessEnvironmentVariable(keyValuePair.Key, keyValuePair.Value);
        }

        return settings;
    }

    public static T EnableNoDependencies<T>(this T settings)
        where T: MSBuildSettings
    {
        return settings.SetProperty("BuildProjectReferences", false);
    }

    public static DotNetMSBuildSettings EnableNoDependencies(this DotNetMSBuildSettings settings)
    {
        return settings.SetProperty("BuildProjectReferences", false);
    }

    public static DotNetTestSettings EnableCrashDumps(this DotNetTestSettings settings, MiniDumpType dumpType = MiniDumpType.MiniDumpWithFullMemory)
    {
        if (bool.Parse(Environment.GetEnvironmentVariable("enable_crash_dumps") ?? "false"))
        {
            return settings
                .SetProcessEnvironmentVariable("COMPlus_DbgEnableMiniDump", "1")
                .SetProcessEnvironmentVariable("COMPlus_DbgMiniDumpType", ((int) dumpType).ToString());
        }

        return settings;
    }

    public static DotNetTestSettings EnableTrxLogOutput(this DotNetTestSettings settings, string resultsDirectory)
    {
        return settings
            .SetLoggers("trx")
            .SetResultsDirectory(resultsDirectory);
    }

    /// <summary>
    /// GitLab installs MSBuild in a non-standard place that causes issues for Nuke trying to resolve it
    /// </summary>
    public static MSBuildSettings SetMSBuildPath(this MSBuildSettings settings)
    {
        var vsRoot = Environment.GetEnvironmentVariable("VSTUDIO_ROOT");

        // Workaround until Nuke supports VS 2022
        string toolPath;
        try
        {
            toolPath = string.IsNullOrEmpty(vsRoot)
                           ? MSBuildToolPathResolver.Resolve()
                           : Path.Combine(vsRoot, "MSBuild", "Current", "Bin", "MSBuild.exe");
        }
        catch
        {
            Serilog.Log.Information("MSBuild auto-detection failed, checking fallback paths");
            // favour the Preview version and x64 versions first 
            var editions = new[] { "Preview", "Enterprise", "Professional", "Community", "BuildTools", };
            var programFiles = new[] { EnvironmentInfo.SpecialFolder(SpecialFolders.ProgramFiles), EnvironmentInfo.SpecialFolder(SpecialFolders.ProgramFilesX86)! };
            var allPaths = (from edition in editions
                            from root in programFiles
                            select Path.Combine(root, $@"Microsoft Visual Studio\2022\{edition}\MSBuild\Current\Bin\msbuild.exe")).ToList();
            toolPath = allPaths.FirstOrDefault(File.Exists);
            if (toolPath is null)
            {
                Serilog.Log.Error("Error locating MSBuild. Checked the following paths:{Paths}", string.Join(Environment.NewLine, allPaths));
                throw new ArgumentException("Error locating MSBuild");
            }
        }

        Serilog.Log.Information("Using MSBuild path '{MSBuild}'", toolPath);

        return settings.SetProcessToolPath(toolPath);
    }

    /// <summary>
    /// Conditionally set the dotnet.exe location, using the 32-bit dll when targeting x86
    /// </summary>
    public static T SetDotnetPath<T>(this T settings, MSBuildTargetPlatform platform)
        where T : ToolSettings
    {
        var dotnetPath = GetDotNetPath(platform);
        return settings.SetProcessToolPath(dotnetPath);
    }

    /// <summary>
    /// Get the path to `dotnet` depending on the platform
    /// </summary>
    public static string GetDotNetPath(MSBuildTargetPlatform platform)
    {
        if (!MSBuildTargetPlatform.x86.Equals(platform))
            return DotNetTasks.DotNetPath;

        var dotnetPath = EnvironmentInfo.GetVariable<string>("DOTNET_EXE_32")
                 ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe");

        if (!File.Exists(dotnetPath))
        {
            throw new Exception($"Error locating 32-bit dotnet process. Expected at '{dotnetPath}'");
        }
        return dotnetPath;
    }

    public static T SetTestTargetPlatform<T>(this T settings, MSBuildTargetPlatform platform)
        where T : ToolSettings
    {
        // To avoid annoying differences in the test code, convert the MSBuildTargetPlatform string values to
        // the same values returned by Environment.Platform(), and skip unsupported values (e.g. MSIL, arm)
        var strPlatform = platform.ToString().ToLowerInvariant();
        var target = strPlatform switch
        {
            "x86" => "X86",
            "x64" => "X64",
            "arm64" => "ARM64",
            _ => throw new InvalidOperationException($"Should only use x64, x86 or ARM64 for Test target platform. (Invalid : {strPlatform})"),
        };

        return settings.SetProcessEnvironmentVariable("TargetPlatform", target);
    }

    /// <summary>
    /// Set filters for tests to ignore
    /// </summary>
    public static T SetIgnoreFilter<T>(this T settings, string[] testsToIgnore)
        where T : DotNetTestSettings
    {
        if (testsToIgnore != null && testsToIgnore.Any())
        {
            var sb = new StringBuilder();
            foreach (var testToIgnore in testsToIgnore)
            {
                sb.Append("FullyQualifiedName!=");
                sb.Append(testToIgnore);
                sb.Append(value: '&');
            }

            sb.Remove(sb.Length - 1, 1);

            settings = settings.SetFilter(sb.ToString());
        }

        return settings;
    }

    public static DotNetTestSettings WithMemoryDumpAfter(this DotNetTestSettings settings, int timeoutInMinutes)
    {
        return settings.SetProcessArgumentConfigurator(
            args =>
                args.Add("--blame-hang")
                    .Add("--blame-hang-dump-type full")
                    .Add($"--blame-hang-timeout {timeoutInMinutes}m")
        );
    }

    public static DotNetTestSettings WithDatadogLogger(this DotNetTestSettings settings)
    {
        var enabled = NukeBuild.IsServerBuild;
        try
        {
            var envVar = Environment.GetEnvironmentVariable("DD_LOGGER_ENABLED");
            if (!string.IsNullOrWhiteSpace(envVar))
            {
                enabled = !string.Equals(envVar, "false", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Security issues...
        }

        if (enabled)
        {
            return settings
                  .SetProcessEnvironmentVariable("DD_LOGGER_BUILD_SOURCESDIRECTORY", NukeBuild.RootDirectory)
                  .SetProcessEnvironmentVariable("DD_CIVISIBILITY_CODE_COVERAGE_SNK_FILEPATH", NukeBuild.RootDirectory / "Datadog.Trace.snk")
                  .SetSettingsFile(NukeBuild.RootDirectory / "tracer" / "test" / "test.settings");
        }

        return settings;
    }

    public static T SetLocalOsxEnvironmentVariables<T>(this T toolSettings)
        where T : ToolSettings
    {
        return toolSettings
              .SetProcessEnvironmentVariable("MONGO_HOST", "localhost")
              .SetProcessEnvironmentVariable("SERVICESTACK_REDIS_HOST", "localhost:6379")
              .SetProcessEnvironmentVariable("STACKEXCHANGE_REDIS_HOST", "localhost:6392,127.0.0.1:6390")
              .SetProcessEnvironmentVariable("STACKEXCHANGE_REDIS_SINGLE_HOST", "localhost:6391")
              .SetProcessEnvironmentVariable("ELASTICSEARCH7_HOST", "localhost:9200")
              .SetProcessEnvironmentVariable("ELASTICSEARCH6_HOST", "localhost:9200")
              .SetProcessEnvironmentVariable("ELASTICSEARCH5_HOST", "localhost:9200")
              .SetProcessEnvironmentVariable("SQLSERVER_CONNECTION_STRING", "Server=localhost;User=sa;Password=Strong!Passw0rd")
              .SetProcessEnvironmentVariable("POSTGRES_HOST", "localhost")
              .SetProcessEnvironmentVariable("MYSQL_HOST", "localhost")
              .SetProcessEnvironmentVariable("MYSQL_PORT", "3306")
              .SetProcessEnvironmentVariable("RABBITMQ_HOST", "localhost")
              .SetProcessEnvironmentVariable("AWS_SDK_HOST", "localhost:4566");
    }
}
