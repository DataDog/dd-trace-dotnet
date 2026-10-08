// <copyright file="AotJsonModelDescriptorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The models Datadog.Trace (de)serializes with Newtonsoft, which a NativeAOT publish must keep, are found in its IL.
/// </summary>
public class AotJsonModelDescriptorTests
{
    private static readonly (HashSet<TypeDef> Types, SortedSet<string> Instantiations) Models
        = JsonModelDescriptor.Collect(ModuleDefMD.Load(typeof(Tracer).Assembly.Location));

    [Theory]
    [InlineData("Datadog.Trace.Iast.VulnerabilityBatch")] // JsonHelper.SerializeObject(this, ...)
    [InlineData("Datadog.Trace.Iast.Vulnerability")] // its properties
    [InlineData("Datadog.Trace.Sampling.LocalCustomSamplingRule/RuleConfigJsonModel")] // DeserializeObject<List<T>>
    [InlineData("Datadog.Trace.Telemetry.TelemetryData")] // JsonSerializer.Serialize(writer, data) and PostAsJsonAsync<T>
    [InlineData("Datadog.Trace.Telemetry.AppStartedPayload")] // an implementation of IPayload, the type of a property
    [InlineData("Datadog.Trace.RemoteConfigurationManagement.Protocol.GetRcmResponse")]
    public void ModelsArePreserved(string type)
        => Models.Types.Select(t => t.FullName).Should().Contain(type);

    [Fact]
    public void VendoredTypesAreNotPreserved()
        => Models.Types.Should().NotContain(t => t.FullName.StartsWith("Datadog.Trace.Vendors"));

    [Fact]
    public void CollectionsNewtonsoftCreatesAreListed()
        => Models.Instantiations.Should().Contain("System.Collections.Generic.List`1[[Datadog.Trace.Sampling.LocalCustomSamplingRule+RuleConfigJsonModel, Datadog.Trace]]");
}
#endif
