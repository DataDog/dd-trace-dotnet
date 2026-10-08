// <copyright file="AotReflectionRootDescriptorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The types of the instrumented libraries the integrations find by name, which a NativeAOT publish must keep with their
/// members, are found in the IL of Datadog.Trace.
/// </summary>
public class AotReflectionRootDescriptorTests
{
    private static readonly HashSet<(string TypeName, string? AssemblyName)> Names
        = ReflectionRootDescriptor.CollectTypeNames(ModuleDefMD.Load(typeof(Tracer).Assembly.Location));

    [Theory]
    [InlineData("RabbitMQ.Client.BasicProperties", null)] // Assembly.GetType, then GetConstructor
    [InlineData("Serilog.Events.ScalarValue", null)]
    [InlineData("Amazon.SQS.Model.MessageAttributeValue", null)]
    [InlineData("OpenTelemetry.Trace.TracerProviderBuilderExtensions", "OpenTelemetry")] // Type.GetType, then GetMethod
    public void TypeNamesAreCollected(string typeName, string? assemblyName)
        => Names.Should().Contain((typeName, assemblyName));

    [Fact]
    public void TypesOfTheApplicationArePreserved()
    {
        var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
        var diagnosticSource = ModuleDefMD.Load(typeof(System.Diagnostics.Activity).Assembly.Location);
        var path = Path.GetTempFileName();
        try
        {
            ReflectionRootDescriptor.Write(datadogTrace, [datadogTrace, diagnosticSource], path).Should().BeGreaterThan(0);

            var assemblies = XDocument.Load(path).Root!.Elements("assembly").ToList();
            assemblies.Select(a => a.Attribute("fullname")!.Value).Should().Equal("System.Diagnostics.DiagnosticSource");
            assemblies[0].Elements("type").Should().Contain(t => t.Attribute("fullname")!.Value == "System.Diagnostics.Activity" && t.Attribute("preserve")!.Value == "all");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
#endif
