// <copyright file="AotNativeTracerHostTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.Runner.Aot.Native;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The native tracer hosted offline: the host emulates the CLR profiling and metadata APIs over the modules it loads, and the
/// native tracer rewrites the methods its embedded CallTarget definitions instrument, like the CLR profiler does at runtime.
/// Needs the native tracer library built from this branch (<c>DD_AOT_NATIVE_TRACER</c>).
/// </summary>
public class AotNativeTracerHostTests
{
    private const uint TracingCategory = 1;
    private const uint Net60TargetFramework = 8;

    [SkippableFact]
    public void TheNativeTracerRewritesTheMethodsItInstrumentsInTheModulesTheHostLoads()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        Skip.If(string.IsNullOrEmpty(nativeTracer), "DD_AOT_NATIVE_TRACER (the path of the native tracer library) is required");

        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var logDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-host-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var host = NativeTracerHost.Create(nativeTracer!, Environment.Version, logDirectory);
            host.EnableEmbeddedDefinitions(TracingCategory, Net60TargetFramework).Should().BeGreaterThan(0);

            host.LoadModule(typeof(object).Assembly.Location, writable: false);
            host.LoadModule(typeof(Tracer).Assembly.Location, writable: false);
            var http = host.LoadModule(Path.Combine(runtimeDirectory, "System.Net.Http.dll"), writable: true);
            host.LoadReferenceClosure(new[] { runtimeDirectory });

            host.ProcessReJitRequests(TimeSpan.FromSeconds(1)).Should().BeGreaterThan(0);

            // The HttpClient integration instruments the SendAsync methods of the handlers: their new bodies call CallTarget.
            var rewritten = MethodBodies.ApplyNewBodies(http);
            rewritten.Should().Contain(method => method.Name == "SendAsync");
            var sendAsync = rewritten.First(method => method.Name == "SendAsync");
            sendAsync.Body.Instructions.Select(instruction => instruction.Operand?.ToString() ?? string.Empty)
                     .Should().Contain(operand => operand.Contains("Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker"));
        }
        finally
        {
            try
            {
                Directory.Delete(logDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
#endif
