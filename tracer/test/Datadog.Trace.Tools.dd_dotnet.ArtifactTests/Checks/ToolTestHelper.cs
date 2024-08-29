// <copyright file="ToolTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests.Checks;

public abstract class ToolTestHelper : TestHelper
{
    protected ToolTestHelper(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
    }

    protected ToolTestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides, output)
    {
    }

    protected async Task<(string StandardOutput, string ErrorOutput, int ExitCode)> RunTool(string arguments, params (string Key, string Value)[] environmentVariables)
    {
        var process = RunToolInteractive(arguments, environmentVariables);

        using var helper = new ProcessHelper(process);

        await helper.Task;

        return (SplitOutput(helper.StandardOutput), SplitOutput(helper.ErrorOutput), helper.Process.ExitCode);

        static string SplitOutput(string output)
        {
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            return string.Join(" ", lines.Select(o => o.TrimEnd()));
        }
    }

    protected Process RunToolInteractive(string arguments, params (string Key, string Value)[] environmentVariables)
    {
        var rid = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentHelper.IsAlpine()) switch
        {
            ("win", _, _) => "win-x64",
            ("linux", "Arm64", false) => "linux-arm64",
            ("linux", "Arm64", true) => "linux-musl-arm64",
            ("linux", "X64", false) => "linux-x64",
            ("linux", "X64", true) => "linux-musl-x64",
            _ => throw new PlatformNotSupportedException()
        };

        var executable = Path.Combine(EnvironmentHelper.MonitoringHome, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dd-dotnet.cmd" : "dd-dotnet.sh");
        Output.WriteLine($"{executable} {arguments}");

        var processStart = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        // Prevent Spectre.Console from inserting fancy control codes
        processStart.EnvironmentVariables["TERM"] = string.Empty;

        foreach (var (key, value) in environmentVariables)
        {
            processStart.EnvironmentVariables[key] = value;
        }

        return Process.Start(processStart);
    }
}
