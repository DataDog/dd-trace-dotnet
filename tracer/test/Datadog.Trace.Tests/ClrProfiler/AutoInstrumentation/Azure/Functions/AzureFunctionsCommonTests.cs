// <copyright file="AzureFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1649 // File name should match first type name
namespace Microsoft.Azure.Functions.Worker.Context.Features
{
    internal interface IFunctionBindingsFeature
    {
    }
}
#pragma warning restore SA1649

#pragma warning disable SA1403 // File may only contain a single namespace
namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.Functions
{
#pragma warning restore SA1403

    [Collection(nameof(TracerInstanceTestCollection))]
    public class AzureFunctionsCommonTests
    {
        // Guards the wiring between AzureInfo.IsAzureContainerApp and the tag prefix actually
        // emitted. The tag-level tests take the bool directly, so without this a refactor could
        // disconnect detection from construction and both suites would still pass.
        [Theory]
        [InlineData(null, "aas.", "aca.")]
        [InlineData("my-container-app", "aca.", "aas.")]
        [EnvironmentRestorer(PlatformKeys.AzureAppService.ContainerAppName)]
        public async Task OnIsolatedFunctionBegin_TagPrefixFollowsContainerAppsDetection(string? containerAppName, string prefix, string otherPrefix)
        {
            Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppName, containerAppName);

            await using var tracer = TracerHelper.CreateWithFakeAgent();

            var functionContext = CreateMockFunctionContext("httpTrigger");

            // A fresh AzureInfo reads the environment above, and the tracer is passed explicitly, so
            // this never touches global state. The production overload supplies the singletons.
            var state = AzureFunctionsCommon.OnIsolatedFunctionBegin(functionContext, triggerType: null, startTime: null, new AzureInfo(), tracer);

            var scope = state.Scope.Should().BeOfType<Scope>().Subject;

            var tags = scope.Span.Tags;
            tags.GetTag(prefix + "function.name").Should().Be("TestFunction");
            tags.GetTag(prefix + "function.method").Should().Be("Tests.TestFunction.Run");
            tags.GetTag(prefix + "function.trigger").Should().Be("Http");

            tags.GetTag(otherPrefix + "function.name").Should().BeNull();
            tags.GetTag(otherPrefix + "function.method").Should().BeNull();
            tags.GetTag(otherPrefix + "function.trigger").Should().BeNull();
        }

        [Theory]
        [InlineData("00", null, null, null)]
        [InlineData("00", "vendor=value", null, "vendor=value")]
        [InlineData("00", "dd=s:0", 0, null)]
        [InlineData("00", "dd=s:-1", -1, null)]
        [InlineData("00", "dd=s:1;p:0000000000000002", 1, null)]
        [InlineData("01", null, 1, null)]
        public void DurableExtractPropagatedContext_UsesWorkerTraceContext(
            string traceFlags,
            string? traceState,
            int? expectedSamplingPriority,
            string? expectedAdditionalTraceState)
        {
            const string traceId = "00000000000000000000000000000001";
            const string spanId = "0000000000000002";
            var context = CreateMockFunctionContext("activityTrigger");
            context.TraceContext = new MockWorkerTraceContext
                                   {
                                       TraceParent = $"00-{traceId}-{spanId}-{traceFlags}",
                                       TraceState = traceState,
                                   }.DuckCast<IWorkerTraceContext>();

            var extractedContext = AzureFunctionsDurablePropagation.ExtractPropagatedContext(context);

            extractedContext.SpanContext.Should().NotBeNull();
            extractedContext.SpanContext!.RawTraceId.Should().Be(traceId);
            extractedContext.SpanContext.RawSpanId.Should().Be(spanId);
            extractedContext.SpanContext.SamplingPriority.Should().Be(expectedSamplingPriority);
            extractedContext.SpanContext.AdditionalW3CTraceState.Should().Be(expectedAdditionalTraceState);
        }

        [Theory]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-00", "00-00000000000000000000000000000001-0000000000000001-01")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-02", "00-00000000000000000000000000000001-0000000000000001-03")]
        [InlineData("00-00000000000000000000000000000001-0000000000000001-01", "00-00000000000000000000000000000001-0000000000000001-01")]
        [InlineData("invalid", "invalid")]
        public void DurableEnsureTraceParentSampledFlag_PreservesOtherFlagsAndInvalidInput(string traceParent, string expected)
        {
            AzureFunctionsDurablePropagation.EnsureTraceParentSampledFlag(traceParent).Should().Be(expected);
        }

        // In-process functions go through OnFunctionExecutionBegin/CreateScope, a separate path from
        // the isolated worker above, so the prefix selection needs its own coverage.
        [Theory]
        [InlineData(null, "aas.", "aca.")]
        [InlineData("my-container-app", "aca.", "aas.")]
        [EnvironmentRestorer(PlatformKeys.AzureAppService.ContainerAppName)]
        public async Task OnFunctionExecutionBegin_TagPrefixFollowsContainerAppsDetection(string? containerAppName, string prefix, string otherPrefix)
        {
            Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppName, containerAppName);

            await using var tracer = TracerHelper.CreateWithFakeAgent();

            var functionInstance = new MockFunctionInstance
            {
                Reason = AzureFunctionsExecutionReason.HostCall,
                BindingSource = new object(),
                FunctionDescriptor = new MockFunctionDescriptor
                {
                    ShortName = "TestFunction",
                    FullName = "Tests.TestFunction.Run",
                },
            };

            var state = AzureFunctionsCommon.OnFunctionExecutionBegin<object?, MockFunctionInstance>(null, functionInstance, new AzureInfo(), tracer);

            var scope = state.Scope.Should().BeOfType<Scope>().Subject;

            var tags = scope.Span.Tags;
            tags.GetTag(prefix + "function.name").Should().Be("TestFunction");
            tags.GetTag(prefix + "function.method").Should().Be("Tests.TestFunction.Run");
            tags.GetTag(prefix + "function.trigger").Should().Be("Http");

            tags.GetTag(otherPrefix + "function.name").Should().BeNull();
            tags.GetTag(otherPrefix + "function.method").Should().BeNull();
            tags.GetTag(otherPrefix + "function.trigger").Should().BeNull();
        }

        private static MockFunctionContext CreateMockFunctionContext(string bindingType)
        {
            var inputBindings = new Hashtable
            {
                ["trigger"] = new MockBindingMetadata
                {
                    Direction = BindingDirection.In,
                    Type = bindingType,
                }
            };

            return new MockFunctionContext
            {
                FunctionDefinition = new FunctionDefinitionStruct
                {
                    InputBindings = inputBindings,
                    Name = "TestFunction",
                    EntryPoint = "Tests.TestFunction.Run",
                }
            };
        }

        // This duck types with the isolated-worker FunctionContext contracts.
        internal class MockFunctionContext : IFunctionContext
        {
            public FunctionDefinitionStruct FunctionDefinition { get; set; }

            public IEnumerable<KeyValuePair<Type, object?>>? Features { get; set; }

            public IDictionary<object, object?>? Items { get; }

            public IWorkerTraceContext? TraceContext { get; set; }
        }

        // This duck types with tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/GrpcBindingsFeatureStruct.cs
        internal class MockBindingsFeature
        {
            public IDictionary<string, object?>? TriggerMetadata { get; set; }

            public IDictionary<string, object?>? InputData { get; set; }
        }

        private class MockBindingMetadata
        {
            public BindingDirection Direction { get; set; }

            public string? Type { get; set; }
        }

        private class MockWorkerTraceContext
        {
            public string? TraceParent { get; set; }

            public string? TraceState { get; set; }
        }

        // Duck types with the in-process Microsoft.Azure.WebJobs.Host contracts.
        private class MockFunctionInstance : IFunctionInstance
        {
            public Guid Id { get; set; }

            public IDictionary<string, string> TriggerDetails { get; set; } = new Dictionary<string, string>();

            public AzureFunctionsExecutionReason Reason { get; set; }

            public object BindingSource { get; set; } = new object();

            public IFunctionDescriptor FunctionDescriptor { get; set; } = new MockFunctionDescriptor();
        }

        private class MockFunctionDescriptor : IFunctionDescriptor
        {
            public string Id { get; set; } = string.Empty;

            public string FullName { get; set; } = string.Empty;

            public string ShortName { get; set; } = string.Empty;

            public string LogName { get; set; } = string.Empty;
        }
    }
}

#endif
