// <copyright file="AotDatadogTraceAnalysisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The analysis of a Datadog.Trace build `dd-trace aot prepare` writes next to it is the one the instrumentation uses for that
/// build, and gives the same descriptors and declarations as analyzing Datadog.Trace again.
/// </summary>
public class AotDatadogTraceAnalysisTests
{
    [Fact]
    public void TheAnalysisNextToDatadogTraceIsUsedForItsBuildOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-aot-analysis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var datadogTracePath = Path.Combine(directory, "Datadog.Trace.dll");
            File.Copy(typeof(Tracer).Assembly.Location, datadogTracePath);
            using var datadogTrace = ModuleDefMD.Load(File.ReadAllBytes(datadogTracePath));
            var analysis = DatadogTraceAnalysis.Create(datadogTrace, datadogTracePath);
            analysis.JsonModels.Should().BeGreaterThan(0);
            analysis.DuckTypeMappings.Should().NotBeEmpty();

            // The written analysis is read back for this build (marked to tell it from a new one)...
            analysis.JsonModels = -1;
            analysis.Write(Path.Combine(directory, DatadogTraceAnalysis.FileName));
            DatadogTraceAnalysis.Get(datadogTrace, datadogTracePath).JsonModels.Should().Be(-1);

            // ... not for another build of Datadog.Trace.
            analysis.DatadogTraceModuleVersionId = Guid.NewGuid();
            analysis.Write(Path.Combine(directory, DatadogTraceAnalysis.FileName));
            DatadogTraceAnalysis.Get(datadogTrace, datadogTracePath).JsonModels.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheAnalysisGivesWhatAnalyzingDatadogTraceGives()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-aot-analysis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var datadogTracePath = typeof(Tracer).Assembly.Location;
            using var datadogTrace = ModuleDefMD.Load(datadogTracePath);
            var analysisPath = Path.Combine(directory, DatadogTraceAnalysis.FileName);
            DatadogTraceAnalysis.Create(datadogTrace, datadogTracePath).Write(analysisPath);
            var analysis = JsonConvert.DeserializeObject<DatadogTraceAnalysis>(File.ReadAllText(analysisPath))!;

            analysis.WriteJsonModels(Path.Combine(directory, "analysis.linker.xml"), Path.Combine(directory, "analysis.rd.xml"))
                    .Should().Be(JsonModelDescriptor.Write(datadogTrace, Path.Combine(directory, "direct.linker.xml"), Path.Combine(directory, "direct.rd.xml")));
            SameXml(directory, "analysis.linker.xml", "direct.linker.xml");
            SameXml(directory, "analysis.rd.xml", "direct.rd.xml");

            analysis.WriteDelegateWrappers(Path.Combine(directory, "analysis.delegates.rd.xml"))
                    .Should().Be(DelegateWrapperDirectives.Write(datadogTrace, Path.Combine(directory, "direct.delegates.rd.xml")));
            SameXml(directory, "analysis.delegates.rd.xml", "direct.delegates.rd.xml");

            analysis.ReflectionTypeNames.Select(n => (n.Name, n.Assembly)).Should().BeEquivalentTo(ReflectionRootDescriptor.CollectTypeNames(datadogTrace));
            analysis.DuckTypeMappings.Select(m => m.ToMapping().Key).Should().Equal(DuckTypeAotAttributeDiscovery.Discover([datadogTracePath]).Mappings.Select(m => m.Key));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static void SameXml(string directory, string first, string second)
            => XNode.DeepEquals(XDocument.Load(Path.Combine(directory, first)), XDocument.Load(Path.Combine(directory, second))).Should().BeTrue($"{first} and {second} should be the same");
    }
}
#endif
