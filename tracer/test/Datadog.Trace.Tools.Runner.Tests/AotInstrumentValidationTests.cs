// <copyright file="AotInstrumentValidationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The options of <c>dd-trace aot instrument</c> are checked before the native tracer is loaded.
/// </summary>
public class AotInstrumentValidationTests
{
    [Fact]
    public void ADatadogTraceOfAnotherVersionIsRejected()
    {
        // An application that references the Datadog.Trace package of 2.x brings the whole tracer of that version.
        var directory = Path.Combine(Path.GetTempPath(), "dd-aot-validation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var datadogTrace = Path.Combine(directory, "Datadog.Trace.dll");
            var module = new ModuleDefUser("Datadog.Trace.dll") { Kind = ModuleKind.Dll };
            new AssemblyDefUser("Datadog.Trace", new Version(2, 1, 0, 0)).Modules.Add(module);
            module.Write(datadogTrace);
            var report = Path.Combine(directory, "report.json");

            var exitCode = AotInstrumentProcessor.Process(new AotInstrumentOptions
            {
                NativeTracerPath = datadogTrace, // only its existence is checked first
                DatadogTracePath = datadogTrace,
                Assemblies = [typeof(AotInstrumentValidationTests).Assembly.Location],
                ReferenceDirectories = [RuntimeEnvironment.GetRuntimeDirectory()],
                OutputDirectory = Path.Combine(directory, "output"),
                ReportPath = report,
            });

            exitCode.Should().Be(1);
            var expected = typeof(Tracer).Assembly.GetName().Version!.ToString(3);
            JObject.Parse(File.ReadAllText(report))["Errors"]!.ToString()
                   .Should().Contain($"references Datadog.Trace 2.1.0").And.Contain($"Reference the Datadog.Trace {expected} package");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
#endif
