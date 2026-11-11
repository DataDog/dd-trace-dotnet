// <copyright file="EnvironmentHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers;

public class EnvironmentHelperTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GenerateLoaderConfigFile_WithoutTracer_OnlyLoadsProfiler(bool enableProfiler, bool setTracerEnvironmentVariables)
    {
        var environment = new EnvironmentHelper("net11.0", enableTracer: false, enableProfiler);
        if (setTracerEnvironmentVariables)
        {
            var tracerHome = Path.GetDirectoryName(Path.GetDirectoryName(environment.GetTracerNativeLibraryPath()))!;
            environment.SetVariable("DD_DOTNET_TRACER_HOME", tracerHome);
            environment.SetVariable("DD_TRACE_ENABLED", "1");
        }

        var profilerPath = environment.GetProfilerNativeLibraryPath();
        var architecture = Path.GetFileName(Path.GetDirectoryName(profilerPath));
        var configFile = environment.GenerateLoaderConfigFile();

        try
        {
            Assert.Equal(
                [$"PROFILER;{{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}};{architecture};{profilerPath}"],
                File.ReadAllLines(configFile));
        }
        finally
        {
            File.Delete(configFile);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void GenerateLoaderConfigFile_WithTracer_LoadsBothComponents(bool enableTracerInConstructor, bool enableProfiler, bool disableTracing)
    {
        var environment = new EnvironmentHelper("net11.0", enableTracerInConstructor, enableProfiler);
        if (!enableTracerInConstructor)
        {
            environment.EnableTracer();
        }

        if (disableTracing)
        {
            environment.SetVariable("DD_TRACE_ENABLED", "0");
        }

        var profilerPath = environment.GetProfilerNativeLibraryPath();
        var tracerPath = environment.GetTracerNativeLibraryPath();
        var architecture = Path.GetFileName(Path.GetDirectoryName(profilerPath));
        var configFile = environment.GenerateLoaderConfigFile();

        try
        {
            Assert.Equal(
                [
                    $"PROFILER;{{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}};{architecture};{profilerPath}",
                    $"TRACER;{{50DA5EED-F1ED-B00B-1055-5AFE55A1ADE5}};{architecture};{tracerPath}"
                ],
                File.ReadAllLines(configFile));
        }
        finally
        {
            File.Delete(configFile);
        }
    }
}
