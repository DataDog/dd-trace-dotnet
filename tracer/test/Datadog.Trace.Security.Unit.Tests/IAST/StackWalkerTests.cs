// <copyright file="StackWalkerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Iast;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class StackWalkerTests
    {
        [Theory]
        [InlineData("System", true)]
        [InlineData("SystemOfGame", false)]
        [InlineData("systemofgame", false)]
        [InlineData("System.OfGame", true)]
        [InlineData("system.ofgame", true)]
        [InlineData("Datadog.Trace", true)]
        [InlineData("MySqlConnector", true)]
        [InlineData("MySqlHelper", false)]
        public void CheckAssemblyExclussion(string assemblyName, bool outcome)
        {
            // we check twice to make sure that the cache does not change the outcome
            StackWalker.MustSkipAssembly(assemblyName).Should().Be(outcome);
            StackWalker.MustSkipAssembly(assemblyName).Should().Be(outcome);
        }

        [Fact]
        public void GivenAStack_WhenGetStackTrace_ThenTheTargetFrameIsTheFirstNonExcludedCaller()
        {
            var stack = Capture();

            stack.Should().NotBeNull();
            StackWalker.TryGetFrame(stack!, out var frame).Should().BeTrue();
            frame!.GetMethod()!.DeclaringType.Should().Be(typeof(StackWalkerTests));
            frame.GetMethod()!.Name.Should().Be(nameof(GivenAStack_WhenGetStackTrace_ThenTheTargetFrameIsTheFirstNonExcludedCaller));
        }

        [Fact]
        public void GivenExcludedFramesBeforeTheTarget_WhenTryGetFrame_ThenTheyAreSkipped()
        {
            // Lazy<T> lives in an excluded assembly, so the target is not simply the first frame.
            var stack = new Lazy<StackTrace?>(Capture).Value;

            stack.Should().NotBeNull();
            StackWalker.TryGetFrame(stack!, out var frame).Should().BeTrue();
            frame!.GetMethod()!.Name.Should().Be(nameof(GivenExcludedFramesBeforeTheTarget_WhenTryGetFrame_ThenTheyAreSkipped));
        }

        [Fact]
        public void GivenANearlyExhaustedStack_WhenGetStackTrace_ThenItBailsOutInsteadOfWalking()
        {
            RecurseUntilTheStackIsNearlyExhausted().Should().BeNull();
        }

        // StackWalker skips its own frame and its immediate caller, so going through this helper
        // leaves the target frame on whoever called it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StackTrace? Capture() => StackWalker.GetStackTrace();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StackTrace? RecurseUntilTheStackIsNearlyExhausted()
        {
            if (ExecutionStackGuard.HasSufficientStack())
            {
                return RecurseUntilTheStackIsNearlyExhausted();
            }

            return StackWalker.GetStackTrace();
        }
    }
}
