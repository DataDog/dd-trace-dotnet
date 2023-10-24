// <copyright file="ProcessInfoTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.dd_dotnet.Checks;
using Datadog.Trace.Tools.Shared;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.dd_dotnet.Tests
{
    public class ProcessInfoTests
    {
        [Fact]
        public void DetectNetFramework()
        {
            var process = new ProcessInfo(
                "app.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: "app.exe",
                GetModulesForNetFramework());

            process.DotnetRuntime.Should().Be(ProcessInfo.Runtime.NetFx);
        }

        [Fact]
        public void DetectNetCore()
        {
            var process = new ProcessInfo(
                "app.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: "app.exe",
                GetModulesForNetCore());

            process.DotnetRuntime.Should().Be(ProcessInfo.Runtime.NetCore);
        }

        [Fact]
        public void DetectMixedRuntimes()
        {
            var process = new ProcessInfo(
                "app.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: "app.exe",
                GetModulesForNetFramework().Concat(GetModulesForNetCore()).ToArray());

            process.DotnetRuntime.Should().Be(ProcessInfo.Runtime.Mixed);
        }

        [Fact]
        public void DetectionFallback()
        {
            var process = new ProcessInfo(
                "app.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: "app.exe",
                new[] { "someModule.dll" });

            process.DotnetRuntime.Should().Be(ProcessInfo.Runtime.Unknown);
        }

        [Fact]
        public void UseEnvironment()
        {
            var process = new ProcessInfo(
                "CallDatadogConfigJson.exe",
                1,
                new Dictionary<string, string>() { { "DD_TRACE_AGENT_URL", "http://environment/" } },
                mainModule: Path.Combine(Environment.CurrentDirectory, "noAppConfig.exe"),
                GetModulesForNetFramework());

            var settings = new ExporterSettings(process.ExtractConfigurationSource(null, null));

            settings.AgentUri.Should().Be("http://environment/");
        }

        [Fact]
        public void UseJsonConfigFromAppConfig()
        {
            var process = new ProcessInfo(
                "CallDatadogConfigJson.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: Path.Combine(Environment.CurrentDirectory, "CallDatadogConfigJson.exe"),
                GetModulesForNetFramework());

            var settings = new ExporterSettings(process.ExtractConfigurationSource(null, null));

            settings.AgentUri.Should().Be("http://datadogConfig.json/");
        }

        [Fact]
        public void DontUseAppConfigWithNetCore()
        {
            var process = new ProcessInfo(
                "CallDatadogConfigJson.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: Path.Combine(Environment.CurrentDirectory, "CallDatadogConfigJson.exe"),
                GetModulesForNetCore());

            var settings = new ExporterSettings(process.ExtractConfigurationSource(null, null));

            settings.AgentUri.Should().Be("http://datadog.json/");
        }

        [Fact]
        public void UseJsonConfigFromEnvironment()
        {
            var process = new ProcessInfo(
                "CallDatadogConfigJson.exe",
                1,
                new Dictionary<string, string>() { { "DD_TRACE_CONFIG_FILE", "datadogConfig.json" } },
                mainModule: Path.Combine(Environment.CurrentDirectory, "noAppConfig.exe"),
                GetModulesForNetFramework());

            var settings = new ExporterSettings(process.ExtractConfigurationSource(null, null));

            settings.AgentUri.Should().Be("http://datadogConfig.json/");
        }

        [Fact]
        public void UseAppConfig()
        {
            var process = new ProcessInfo(
                "DoNotCallDatadogConfigJson.exe",
                1,
                new Dictionary<string, string>(),
                mainModule: Path.Combine(Environment.CurrentDirectory, "DoNotCallDatadogConfigJson.exe"),
                GetModulesForNetFramework());

            var settings = new ExporterSettings(process.ExtractConfigurationSource(null, null));

            settings.AgentUri.Should().Be("http://app.config/");
        }

        private static string[] GetModulesForNetFramework() => new[] { "clr.dll" };

        private static string[] GetModulesForNetCore() => new[] { "coreclr.dll" };
    }
}
