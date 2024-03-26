// <copyright file="CoverageCollectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceCollector;

using System.IO;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Metadata;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using DatadogTraceConstants = DatadogTraceCollector::Datadog.Trace.Coverage.Collector.DatadogTraceConstants;

namespace Datadog.Trace.Tests;

public class CoverageCollectorTests
{
    [Fact]
    public void DatadogTraceConstants_HasCorrectValues()
    {
        using var scope = new AssertionScope();
        DatadogTraceConstants.AssemblyName.Should().Be(typeof(Tracer).Assembly.GetName().Name);
        DatadogTraceConstants.AssemblyFileName.Should().Be(Path.GetFileName(typeof(Tracer).Assembly.Location));
        DatadogTraceConstants.AssemblyVersion.Should().Be(typeof(Tracer).Assembly.GetName().Version);

        DatadogTraceConstants.Namespaces.ModuleCoverageMetadata.Should().Be(typeof(ModuleCoverageMetadata).Namespace);
        DatadogTraceConstants.TypeNames.ModuleCoverageMetadata.Should().Be(typeof(ModuleCoverageMetadata).FullName);
        DatadogTraceConstants.TypeNames.FileCoverageMetadata.Should().Be(typeof(FileCoverageMetadata).FullName);
        DatadogTraceConstants.TypeNames.CoverageReporter.Should().Be(typeof(CoverageReporter<>).FullName);
    }
}
