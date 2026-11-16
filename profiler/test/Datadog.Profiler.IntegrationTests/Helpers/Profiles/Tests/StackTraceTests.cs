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
        public void EndWith_IsAPrefixCheck()
        {
            var stack = TestStack.Trace("A", "B", "C");

            stack.EndWith(TestStack.Trace("A", "B")).Should().BeTrue();
            stack.EndWith(TestStack.Trace("A", "B", "C")).Should().BeTrue();
            stack.EndWith(TestStack.Trace("B", "C")).Should().BeFalse();
            stack.EndWith(new StackTrace()).Should().BeTrue();

            var tooLong = TestStack.Trace("A", "B", "C", "D");
            stack.EndWith(tooLong, out var tooLongMessage).Should().BeFalse();
            tooLongMessage.Should().Contain("fewer frames");
            tooLongMessage.Should().Contain(stack.ToString());
            tooLongMessage.Should().Contain(tooLong.ToString());

            var mismatch = TestStack.Trace("A", "Z");
            stack.EndWith(mismatch, out var mismatchMessage).Should().BeFalse();
            mismatchMessage.Should().Contain("First mismatch at compared frame index 1");
            mismatchMessage.Should().Contain(mismatch[1].ToString());
            mismatchMessage.Should().Contain(stack[1].ToString());
            mismatchMessage.Should().Contain(stack.ToString());
            mismatchMessage.Should().Contain(mismatch.ToString());
        }

        [Fact]
        public void Contains_StopsAtTheFirstCandidate_AndThrowsOnTheOffByOne()
        {
            var stack = TestStack.Trace("A", "X", "A", "B");

            stack.Contains(TestStack.Trace("A", "X")).Should().BeTrue();
            stack.Contains(TestStack.Trace("A", "B")).Should().BeFalse();
            stack.Contains(TestStack.Trace("X", "A", "B")).Should().BeTrue();
            TestStack.Trace("A").Contains(TestStack.Trace("A", "B")).Should().BeFalse();

            var shortStack = TestStack.Trace("A", "B");
            Action offByOne = () => shortStack.Contains(TestStack.Trace("B", "C"));
            offByOne.Should().Throw<ArgumentOutOfRangeException>();

            Action emptyNeedle = () => stack.Contains(new StackTrace());
            emptyNeedle.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void StartsWith_MatchesEndWith_IncludingTheFailureMessage()
        {
            var stack = TestStack.Trace("A", "B", "C");
            var prefix = TestStack.Trace("A", "B");
            var suffix = TestStack.Trace("B", "C");

            stack.StartsWith(prefix).Should().Be(stack.EndWith(prefix));
            stack.StartsWith(suffix).Should().BeFalse();
            stack.StartsWith(new StackTrace()).Should().BeTrue();

            var mismatch = TestStack.Trace("A", "Z");
            stack.EndWith(mismatch, out var endWithMessage);
            stack.StartsWith(mismatch, out var startsWithMessage).Should().BeFalse();
            startsWithMessage.Should().Be(endWithMessage);
        }

        [Fact]
        public void ContainsSequence_FindsAContiguousSequence_WithoutTheLegacyEdges()
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
                .Which.Message.Should().Contain("First mismatch at compared frame index 1")
                .And.Contain(expected[1].ToString())
                .And.Contain(actual[1].ToString())
                .And.Contain(actual.ToString())
                .And.Contain(expected.ToString());

            var sequenceActual = TestStack.Trace("A", "X", "C");
            var sequenceExpected = TestStack.Trace("A", "B");
            Action sequence = () => sequenceActual.Should().ContainSequence(sequenceExpected);
            sequence.Should().Throw<Exception>()
                .Which.Message.Should().Contain("First mismatch at expected frame index 1")
                .And.Contain(sequenceExpected[1].ToString())
                .And.Contain(sequenceActual[1].ToString())
                .And.Contain(sequenceActual.ToString())
                .And.Contain(sequenceExpected.ToString());
        }
    }
}
