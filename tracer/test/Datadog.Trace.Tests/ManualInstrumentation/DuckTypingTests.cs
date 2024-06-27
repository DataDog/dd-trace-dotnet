// <copyright file="DuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tagging;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Moq;
using Xunit;
using ManualBenchmarkDiscreteStats = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkDiscreteStats;
using ManualBenchmarkHostInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkHostInfo;
using ManualBenchmarkJobInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkJobInfo;
using ManualDuckTypeTargetAttribute = DatadogTraceManual::Datadog.Trace.DuckTyping.DuckTypeTarget;
using ManualIScope = DatadogTraceManual::Datadog.Trace.IScope;
using ManualISpan = DatadogTraceManual::Datadog.Trace.ISpan;
using ManualISpanContext = DatadogTraceManual::Datadog.Trace.ISpanContext;
using ManualITest = DatadogTraceManual::Datadog.Trace.Ci.ITest;
using ManualITestSession = DatadogTraceManual::Datadog.Trace.Ci.ITestSession;
using ManualTestParameters = DatadogTraceManual::Datadog.Trace.Ci.TestParameters;
using TestStatus = DatadogTraceManual::Datadog.Trace.Ci.TestStatus;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class DuckTypingTests
{
    private readonly AsyncLocalScopeManager _scopeManager = new();
    private readonly TracerSettings _settings = new() { StartupDiagnosticLogEnabled = false };
    private readonly Tracer _tracer;

    public DuckTypingTests()
    {
        _tracer = new Tracer(_settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object, scopeManager: _scopeManager, statsd: null);
    }

    [Fact]
    public void CanDuckTypeScopeAsManualIScope()
    {
        var scope = _tracer.StartActiveInternal("manual");
        var span = scope.Span;
        var spanContext = span.Context;

        var manualScope = scope.DuckCast<ManualIScope>();

        // call all the properties to check for duck typing issues
        var manualSpan = manualScope.Span.Should().BeAssignableTo<ManualISpan>().Subject;
        manualSpan.SpanId.Should().Be(span.SpanId);
        manualSpan.SetException(new Exception("MyException"));
        manualSpan.Error.Should().Be(span.Error).And.BeTrue();
        manualSpan.Type.Should().Be(span.Type);
        manualSpan.OperationName.Should().Be(span.OperationName);
        manualSpan.ResourceName.Should().Be(span.ResourceName);
        manualSpan.ServiceName.Should().Be(span.ServiceName);
        manualSpan.TraceId.Should().Be(span.TraceId);
        // This won't return the _same_ object, because it's a struct duck type.
        // Should still refer to the same underlying span though
        var returned = manualSpan.SetTag("Test", "SomeValue");
        returned.Should().BeAssignableTo<IDuckType>().Subject.Instance.Should().BeSameAs(span);
        manualSpan.GetTag("Test").Should().Be("SomeValue");
        span.GetTag("Test").Should().Be("SomeValue"); // check it was mirrored

        var manualSpanContext = manualSpan.Context.Should().BeAssignableTo<ManualISpanContext>().Subject;
        manualSpanContext.SpanId.Should().Be(spanContext.SpanId);
        manualSpanContext.ServiceName.Should().Be(spanContext.ServiceName);
        manualSpanContext.TraceId.Should().Be(spanContext.TraceId);

        manualScope.Close();
        manualScope.Dispose();
    }

    [Fact]
    public void CanDuckTypeManualTestSessionAsISession()
    {
        var autoSession = TestSession.GetOrCreate("blah");

        var session = autoSession.DuckCast<ManualITestSession>();

        // call the methods to make sure it works
        var module = session.CreateModule("somemodule");
        module.Should().NotBeNull();

        var suite = module.GetOrCreateSuite("mysuite");
        suite.Should().NotBeNull();

        var test = suite.CreateTest("mytest");
        test.Should().NotBeNull();

        var stats = new ManualBenchmarkDiscreteStats(100, 100, 100, 100, 100, 0, 0, 0, 0, 100, 100, 100);
        var statsDuckType = stats.DuckCast<IBenchmarkDiscreteStats>();
        TestExtensionsAddBenchmarkDataIntegration.OnMethodBegin<ManualITestSession, ManualITest, IBenchmarkDiscreteStats>(test, BenchmarkMeasureType.RunTime, "some info", statsDuckType);
        // test.AddBenchmarkData(BenchmarkMeasureType.ApplicationLaunch, info: "something", in stats);

        var parameters = new ManualTestParameters { Arguments = new(), Metadata = new() };
        var paramsDuckType = parameters.DuckCast<ITestParameters>();
        TestExtensionsSetParametersIntegration.OnMethodBegin<ManualITestSession, ManualITest, ITestParameters>(test, paramsDuckType);
        // test.SetParameters(new TestParameters { Arguments = new(), Metadata = new() });

        var hostInfo = new ManualBenchmarkHostInfo { RuntimeVersion = "123" };
        var jobInfo = new ManualBenchmarkJobInfo { Description = "weeble" };
        var hostInfoDuckType = hostInfo.DuckCast<IBenchmarkHostInfo>();
        var jobInfoDuckType = jobInfo.DuckCast<IBenchmarkJobInfo>();
        TestExtensionsSetBenchmarkMetadataIntegration.OnMethodBegin<ManualITestSession, ManualITest, IBenchmarkHostInfo, IBenchmarkJobInfo>(test, in hostInfoDuckType, in jobInfoDuckType);
        // test.SetBenchmarkMetadata(new BenchmarkHostInfo() { RuntimeVersion = "123" }, new BenchmarkJobInfo() { Description = "weeble" });

        // basic check that things were pushed down correctly
        var span = test.Should()
                       .BeAssignableTo<IDuckType>()
                       .Subject.Instance.Should()
                       .BeOfType<Test>()
                       .Subject.GetInternalSpan()
                       .Should()
                       .BeOfType<Span>()
                       .Subject;
        span.GetMetric("benchmark.run_time.run").Should().Be(100);

        span.GetTag("host.runtime_version").Should().Be("123");
        span.GetTag("test.configuration.job_description").Should().Be("weeble");

        var tags = span.Tags.Should().BeOfType<TestSpanTags>().Subject;
        tags.Parameters
            .Should()
            .NotBeNull()
            .And.Be(new TestParameters { Arguments = new(), Metadata = new() }.ToJSON());

        test.Close(TestStatus.Pass);
        suite.Close();
        module.Close();
        session.Close(TestStatus.Pass);
    }

    [Fact]
    public void CanDuckTypeAllAnnotatedTypesInDatadogTrace()
    {
        // This test ensures we can do the duck typing without needing to create an instance of the type
        // it misses some checks compared to creating an instance and accessing the properties,
        // but it's a good sanity check
        var targetAssembly = typeof(ManualIScope).Assembly;
        var proxiesAssembly = typeof(Tracer).Assembly;

        TestDuckTypes(proxiesAssembly, targetAssembly);
    }

    [Fact]
    public void CanDuckTypeAllAnnotatedTypesInDatadogTraceManual()
    {
        // This test ensures we can do the duck typing without needing to create an instance of the type
        // it misses some checks compared to creating an instance and accessing the properties,
        // but it's a good sanity check
        var targetAssembly = typeof(Tracer).Assembly;
        var proxiesAssembly = typeof(ManualIScope).Assembly;

        TestDuckTypes(proxiesAssembly, targetAssembly);
    }

    [Fact]
    public void AllTypesInDatadogTraceManualWithADuckTypeTargetAttributeAreDuckTypeAnnotatedInDatadogTrace()
    {
        // This test ensures tht every type that is marked as being duck typed, has a corresponding annotated duck type in the other assembly
        var manualAssembly = typeof(ManualIScope).Assembly;
        var typesWithDuckTypeTargetAttribute =
            manualAssembly
               .GetTypes()
               .Where(
                    type => type
                           .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                           .Any(member => member.GetCustomAttributes<ManualDuckTypeTargetAttribute>().Any()));

        var duckTypeTypes =
            typeof(Tracer)
               .Assembly
               .GetTypes()
               .SelectMany(
                    type => type
                           .GetCustomAttributesData()
                           .Select(attr => GetTarget(attr, "Datadog.Trace.Manual") is { } target ? manualAssembly.GetType(target) : null)
                           .Where(target => target != null))
               .Distinct()
               .ToList();

        typesWithDuckTypeTargetAttribute.Should().BeSubsetOf(duckTypeTypes);
    }

    private void TestDuckTypes(Assembly proxiesAssembly, Assembly targetAssembly)
    {
        var types = proxiesAssembly
                   .GetTypes()
                   .SelectMany(
                        type => type.GetCustomAttributesData()
                                    .Select(attr => GetTarget(attr, targetAssembly.GetName().Name))
                                    .Where(target => target != null)
                                    .Select(target => (type, target)));

        foreach (var (type, target) in types)
        {
            var targetType = targetAssembly.GetType(target);
            if (targetType is null)
            {
                throw new Exception($"Could not find target type: {target} in assembly {targetAssembly} required by duck type {type}");
            }

            var result = DuckType.GetOrCreateProxyType(type, targetType);

            using var s = new AssertionScope();
            s.AddReportable("proxy_type", () => type.ToString());
            s.AddReportable("target_type", target);
            result.Success.Should().BeTrue();
            result.CanCreate().Should().BeTrue();
            FluentActions.Invoking(() => result.ProxyType).Should().NotThrow();
        }
    }

    private string GetTarget(CustomAttributeData attr, string assemblyName)
        => (attr.AttributeType.Name is "DuckTypeAttribute" or "DuckCopyAttribute")
        && attr.ConstructorArguments.Count == 2
        && attr.ConstructorArguments[1].Value?.ToString() == assemblyName
               ? attr.ConstructorArguments[0].Value?.ToString()
               : null;
}
