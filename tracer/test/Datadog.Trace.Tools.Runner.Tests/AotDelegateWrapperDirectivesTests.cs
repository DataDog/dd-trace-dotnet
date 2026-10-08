// <copyright file="AotDelegateWrapperDirectivesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The delegate wrappers DelegateInstrumentation instantiates at runtime, which a NativeAOT publish must compile for each
/// callbacks struct, are found in Datadog.Trace.
/// </summary>
public class AotDelegateWrapperDirectivesTests
{
    private static readonly ModuleDefMD DatadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);

    [Theory]
    [InlineData("Action1Wrapper`3", "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.ProduceDeliveryCallbacks")] // Kafka producer delivery handler
    [InlineData("Action2Wrapper`4", "Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.OffsetsCommittedCallbacks")] // Kafka consumer offsets handler
    public void WrappersAreInstantiatedForTheirCallbacks(string wrapper, string callbacks)
        => DelegateWrapperDirectives.Collect(DatadogTrace).Should().Contain(i => i.Wrapper.Name == wrapper && i.Callbacks.FullName == callbacks);

    [Fact]
    public void ConstraintsAreHonored()
        => DelegateWrapperDirectives.Collect(DatadogTrace).Should().NotContain(i => i.Wrapper.Name == "Action1Wrapper`3" && i.Callbacks.Name == "OffsetsCommittedCallbacks");

    [Fact]
    public void DirectivesUseTheCanonicalFormOfTheOtherArguments()
    {
        var path = Path.GetTempFileName();
        try
        {
            DelegateWrapperDirectives.Write(DatadogTrace, path).Should().BeGreaterThan(0);

            XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
            var types = XDocument.Load(path).Descendants(directives + "Type").Select(t => t.Attribute("Name")!.Value).ToList();
            types.Should().Contain(
                "Datadog.Trace.Util.Delegates.DelegateInstrumentation+Action1Wrapper`3[[System.Object, System.Private.CoreLib],[System.Object, System.Private.CoreLib],[Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka.ProduceDeliveryCallbacks, Datadog.Trace]]");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
#endif
