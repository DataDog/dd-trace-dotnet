// <copyright file="ProcessBasicCheckRunTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests
{
    public class ProcessBasicCheckRunTests
    {
        [Fact]
        public void ReportsBundleInstallInsteadOfInstallerErrorWhenLaunchedAsDotnetDll()
        {
            // Regression scenario: a correctly configured Datadog.Trace.Bundle Nuget install,
            // launched as `dotnet <app>.dll` (e.g. Azure App Service Linux), where MainModule
            // is the dotnet host rather than the app directory. CORECLR_ENABLE_PROFILING is
            // deliberately wrong so the overall check fails and reaches the bundle/installer
            // detection branch.
            var process = new ProcessInfo(
                "dotnet",
                1,
                new Dictionary<string, string>
                {
                    ["CORECLR_PROFILER_PATH"] = "/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                    ["CORECLR_PROFILER"] = Utils.Profilerid,
                    ["CORECLR_ENABLE_PROFILING"] = "0",
                    ["DD_DOTNET_TRACER_HOME"] = "/home/site/wwwroot/datadog",
                },
                mainModule: "/usr/share/dotnet/dotnet",
                modules: new[] { "coreclr.dll" });

            using var console = ConsoleHelper.Redirect();

            var ok = ProcessBasicCheck.Run(process);

            ok.Should().BeFalse();
            console.Output.Should().Contain("Found a Datadog.Trace.Bundle Nuget install");
            console.Output.Should().Contain("Check failing with Datadog.Trace.Bundle Nuget");
            console.Output.Should().NotContain("/opt/datadog");
        }
    }
}
