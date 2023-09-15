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

    protected async Task<string> RunTool(string arguments, params (string Key, string Value)[] environmentVariables)
    {
        var rid = (EnvironmentTools.GetOS(), EnvironmentTools.GetPlatform(), EnvironmentHelper.IsAlpine()) switch
        {
            ("win", _, _) => "win-x64",
            ("linux", "Arm64", _) => "linux-arm64",
            ("linux", "X64", false) => "linux-x64",
            ("linux", "X64", true) => "linux-musl-x64",
            _ => throw new PlatformNotSupportedException()
        };

        var targetFolder = Path.Combine(EnvironmentHelper.MonitoringHome, rid);
        var executable = Path.Combine(targetFolder, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dd-dotnet.exe" : "dd-dotnet");

        var processStart = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var (key, value) in environmentVariables)
        {
            processStart.EnvironmentVariables[key] = value;
        }

        using var helper = new ProcessHelper(Process.Start(processStart));

        await helper.Task;

        var splitOutput = helper.StandardOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        return string.Join(" ", splitOutput.Select(o => o.TrimEnd()));
    }
}
