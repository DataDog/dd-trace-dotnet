// <copyright file="StackFrameBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class StackFrameBuilderTests
    {
        [Fact]
        public void Build_ProducesTheSameTextAsTheNativeFrame_WithEmptyTypeAndFunctionParameters()
        {
            StackFrame frame = StackFrame.Build()
                .Module("Samples.ExceptionGenerator")
                .Namespace("Samples.ExceptionGenerator")
                .Type("ParallelExceptionsScenario")
                .Function("ThrowExceptions")
                .Signature("(object state)");

            frame.ToString().Should().Be("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)");
            frame.Should().Be(new StackFrame("|lm:Samples.ExceptionGenerator |ns:Samples.ExceptionGenerator |ct:ParallelExceptionsScenario |cg: |fn:ThrowExceptions |fg: |sg:(object state)"));
        }

        [Fact]
        public void Build_WritesTypeAndFunctionParameters()
        {
            StackFrame frame = StackFrame.Build()
                .Module("Samples.Computer01")
                .Namespace("Samples.Computer01")
                .Type("GenericClass")
                .TypeParameters("<TKey, TValue>")
                .Function("ThrowFromGeneric")
                .FunctionParameters("<T0>")
                .Signature("(T0 element, TKey key1, TValue value, TKey key2)");

            var expected = "|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:GenericClass |cg:<TKey, TValue> |fn:ThrowFromGeneric |fg:<T0> |sg:(T0 element, TKey key1, TValue value, TKey key2)";
            frame.ToString().Should().Be(expected);
            frame.Should().Be(new StackFrame(expected));
            frame.TypeAdornment.Should().Be("<TKey, TValue>");
            frame.FunctionAdornment.Should().Be("<T0>");
        }

        [Fact]
        public void Build_KeepsAnEmptySignature()
        {
            StackFrame frame = StackFrame.Build()
                .Module("Samples.Computer01")
                .Namespace("Samples.Computer01")
                .Type("LineNumber")
                .Function("CallFirstMethod")
                .Signature("()");

            var expected = "|lm:Samples.Computer01 |ns:Samples.Computer01 |ct:LineNumber |cg: |fn:CallFirstMethod |fg: |sg:()";
            frame.ToString().Should().Be(expected);
            frame.Should().Be(new StackFrame(expected));
        }

        [Fact]
        public void Build_LeavesUnsetPartsEmpty()
        {
            StackFrame frame = StackFrame.Build().Function("Garbage Collector");

            frame.ToString().Should().Be("|lm: |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:");
            frame.Should().Be(new StackFrame("|lm: |ns: |ct: |cg: |fn:Garbage Collector |fg: |sg:"));
        }
    }
}
