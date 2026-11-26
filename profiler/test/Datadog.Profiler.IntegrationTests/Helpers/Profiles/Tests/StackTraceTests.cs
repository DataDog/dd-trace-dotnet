// <copyright file="StackTraceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    [Trait("Category", "TestInfrastructure")]
    public class StackTraceTests
    {
        [Fact]
        public void StartsWith_IsAPrefixCheck()
        {
            var stack = TestStack.Trace("A", "B", "C");

            stack.StartsWith(TestStack.Trace("A", "B")).Should().BeTrue();
            stack.StartsWith(TestStack.Trace("A", "B", "C")).Should().BeTrue();
            stack.StartsWith(TestStack.Trace("B", "C")).Should().BeFalse();
            stack.StartsWith(new StackTrace()).Should().BeTrue();

            var tooLong = TestStack.Trace("A", "B", "C", "D");
            stack.StartsWith(tooLong, out var tooLongMessage).Should().BeFalse();
            tooLongMessage.Should().Contain("fewer frames");
            tooLongMessage.Should().Contain(stack.ToString());
            tooLongMessage.Should().Contain(tooLong.ToString());

            var mismatch = TestStack.Trace("A", "Z");
            stack.StartsWith(mismatch, out var mismatchMessage).Should().BeFalse();
            mismatchMessage.Should().Contain("First mismatch at compared frame index 1");
            mismatchMessage.Should().Contain(mismatch[1].ToString());
            mismatchMessage.Should().Contain(stack[1].ToString());
            mismatchMessage.Should().Contain(stack.ToString());
            mismatchMessage.Should().Contain(mismatch.ToString());
        }

        [Fact]
        public void ContainsSequence_FindsAContiguousSequence()
        {
            var stack = TestStack.Trace("A", "X", "A", "B");

            stack.ContainsSequence(TestStack.Trace("A", "B")).Should().BeTrue();
            stack.ContainsSequence(TestStack.Trace("X", "A")).Should().BeTrue();
            stack.ContainsSequence(TestStack.Trace("A", "C")).Should().BeFalse();
            stack.ContainsSequence(new StackTrace()).Should().BeTrue();
            new StackTrace().ContainsSequence(new StackTrace()).Should().BeTrue();
            TestStack.Trace("A", "B").ContainsSequence(TestStack.Trace("B", "C")).Should().BeFalse();
            new StackTrace().ContainsSequence(TestStack.Trace("A")).Should().BeFalse();
        }

        [Fact]
        public void StartsWith_AndContainsSequence_CompareFramesTheSameWay()
        {
            var actual = new StackTrace(
                StackFrame.Build().Module("Samples").Namespace("Ns").Type("Type").Function("Leaf").Signature("()"),
                StackFrame.Build().Module("Samples").Namespace("Ns").Type("Type").Function("Caller").Signature("()"));
            var same = new StackTrace(
                StackFrame.Build().Module("Samples").Namespace("Ns").Type("Type").Function("Leaf").Signature("()"),
                StackFrame.Build().Module("Samples").Namespace("Ns").Type("Type").Function("Caller").Signature("()"));
            var otherAdornment = new StackTrace(
                StackFrame.Build().Module("Samples").Namespace("Ns").Type("Type").TypeParameters("<T>").Function("Leaf").Signature("()"));

            actual.StartsWith(same).Should().BeTrue();
            actual.ContainsSequence(same).Should().BeTrue();
            actual.StartsWith(otherAdornment).Should().BeFalse();
            actual.ContainsSequence(otherAdornment).Should().BeFalse();
        }

        [Fact]
        public void StackTrace_EnumeratesFramesInOrder()
        {
            var stack = TestStack.Trace("A", "B");
            var functions = new List<string>();
            foreach (var frame in stack)
            {
                functions.Add(frame.Function);
            }

            functions.Should().Equal("A", "B");
            stack.FramesCount.Should().Be(2);
        }

        [Fact]
        public void StackAssertions_PrintTheFirstMismatchAndBothStacks()
        {
            var actual = TestStack.Trace("Leaf", "Caller");
            var expected = TestStack.Trace("Leaf", "Other");

            Action prefix = () => actual.Should().StartWith(expected);
            prefix.Should().Throw<Exception>()
                .Which.Message.Should().ContainAll(
                    "First mismatch at compared frame index 1",
                    expected[1].ToString(),
                    actual[1].ToString(),
                    actual.ToString(),
                    expected.ToString());

            var sequenceActual = TestStack.Trace("A", "X", "C");
            var sequenceExpected = TestStack.Trace("A", "B");
            Action sequence = () => sequenceActual.Should().ContainSequence(sequenceExpected);
            sequence.Should().Throw<Exception>()
                .Which.Message.Should().ContainAll(
                    "First mismatch at expected frame index 1",
                    sequenceExpected[1].ToString(),
                    sequenceActual[1].ToString(),
                    sequenceActual.ToString(),
                    sequenceExpected.ToString());
        }
    }
}
