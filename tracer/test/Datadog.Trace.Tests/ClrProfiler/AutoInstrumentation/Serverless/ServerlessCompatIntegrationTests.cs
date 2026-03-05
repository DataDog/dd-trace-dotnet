// <copyright file="ServerlessCompatIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Serverless
{
    public class ServerlessCompatIntegrationTests : IDisposable
    {
        public ServerlessCompatIntegrationTests()
        {
            // Reset static caches before each test
            ResetStaticField(typeof(CompatibilityLayer_CalculateTracePipeName_Integration), "_cachedTracePipeName");
            ResetStaticField(typeof(CompatibilityLayer_CalculateDogStatsDPipeName_Integration), "_cachedDogStatsDPipeName");
        }

        public void Dispose()
        {
            // Reset static caches after each test to avoid poisoning other tests
            ResetStaticField(typeof(CompatibilityLayer_CalculateTracePipeName_Integration), "_cachedTracePipeName");
            ResetStaticField(typeof(CompatibilityLayer_CalculateDogStatsDPipeName_Integration), "_cachedDogStatsDPipeName");
        }

        [Fact]
        public void TracePipe_OnMethodEnd_ReturnsOriginalValue_WhenExceptionIsNonNull()
        {
            var originalValue = "compat_pipe";
            var exception = new InvalidOperationException("test");
            var state = CallTargetState.GetDefault();

            var result = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
                null!, originalValue, exception, in state);

            result.GetReturnValue().Should().Be(originalValue);
        }

        [Fact]
        public void DogStatsDPipe_OnMethodEnd_ReturnsOriginalValue_WhenExceptionIsNonNull()
        {
            var originalValue = "compat_pipe";
            var exception = new InvalidOperationException("test");
            var state = CallTargetState.GetDefault();

            var result = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
                null!, originalValue, exception, in state);

            result.GetReturnValue().Should().Be(originalValue);
        }

        [Fact]
        public void TracePipe_OnMethodEnd_ReturnsCachedPipeName_AcrossMultipleInvocations()
        {
            var state = CallTargetState.GetDefault();

            var first = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
                null!, "compat1", null!, in state);

            var second = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
                null!, "compat2", null!, in state);

            first.GetReturnValue().Should().Be(second.GetReturnValue());
        }

        [Fact]
        public void DogStatsDPipe_OnMethodEnd_ReturnsCachedPipeName_AcrossMultipleInvocations()
        {
            var state = CallTargetState.GetDefault();

            var first = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
                null!, "compat1", null!, in state);

            var second = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
                null!, "compat2", null!, in state);

            first.GetReturnValue().Should().Be(second.GetReturnValue());
        }

        [Fact]
        public void TracePipe_OnMethodEnd_OverridesCompatLayerValue()
        {
            var compatValue = "compat_trace_pipe";
            var state = CallTargetState.GetDefault();

            var result = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
                null!, compatValue, null!, in state);

            result.GetReturnValue().Should().NotBe(compatValue);
            result.GetReturnValue().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void DogStatsDPipe_OnMethodEnd_OverridesCompatLayerValue()
        {
            var compatValue = "compat_dogstatsd_pipe";
            var state = CallTargetState.GetDefault();

            var result = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
                null!, compatValue, null!, in state);

            result.GetReturnValue().Should().NotBe(compatValue);
            result.GetReturnValue().Should().NotBeNullOrEmpty();
        }

        private static void ResetStaticField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
#endif
