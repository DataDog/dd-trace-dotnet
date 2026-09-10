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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenAStack_WhenTryGetStackTraceAndFrame_ThenTheTargetFrameIsTheFirstNonExcludedCaller(bool captureSourceInfoForAllFrames)
        {
            Capture(captureSourceInfoForAllFrames, out var stack, out var frame).Should().BeTrue();

            stack.Should().NotBeNull();
            frame!.GetMethod()!.DeclaringType.Should().Be(typeof(StackWalkerTests));
            frame.GetMethod()!.Name.Should().Be(nameof(GivenAStack_WhenTryGetStackTraceAndFrame_ThenTheTargetFrameIsTheFirstNonExcludedCaller));
        }

        [Fact]
        public void GivenSourceInfoIsNotCapturedForEveryFrame_WhenTryGetStackTraceAndFrame_ThenTheTargetFrameKeepsItsFileInfo()
        {
            Capture(true, out _, out var frameFromFullCapture).Should().BeTrue();
            Capture(false, out _, out var frameFromCheapCapture).Should().BeTrue();

            frameFromCheapCapture!.GetFileName().Should().Be(frameFromFullCapture!.GetFileName());

            // Only assert on the line when the test assembly actually ships debug info.
            if (frameFromFullCapture.GetFileName() is not null)
            {
                frameFromCheapCapture.GetFileLineNumber().Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void GivenExcludedFramesBeforeTheTarget_WhenTryGetStackTraceAndFrame_ThenTheResolvedFrameMatchesTheCapturedOne()
        {
            // Lazy<T> lives in an excluded assembly, so the target frame sits at an index greater than
            // zero and the cheap capture has to re-resolve it by index rather than by luck.
            var frames = new Lazy<(StackFrame? Full, StackFrame? Cheap)>(CaptureBothFromAnExcludedCaller).Value;

            frames.Full!.GetMethod().Should().BeSameAs(frames.Cheap!.GetMethod());
            frames.Full.GetMethod()!.Name.Should().Be(nameof(GivenExcludedFramesBeforeTheTarget_WhenTryGetStackTraceAndFrame_ThenTheResolvedFrameMatchesTheCapturedOne));
            frames.Cheap.GetFileName().Should().Be(frames.Full.GetFileName());
        }

        [Fact]
        public void GivenANearlyExhaustedStack_WhenTryGetStackTraceAndFrame_ThenItBailsOutInsteadOfWalking()
        {
            RecurseUntilTheStackIsNearlyExhausted().Should().BeFalse();
        }

        // Both the capture and the frame lookup happen inside StackWalker, which skips its own frame
        // and its immediate caller (this helper), so the frame under test is the calling test method.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Capture(bool captureSourceInfoForAllFrames, out StackTrace? stack, out StackFrame? frame)
            => StackWalker.TryGetStackTraceAndFrame(captureSourceInfoForAllFrames, out stack, out frame);

        // Calls StackWalker directly (no local helper) so the two frames it skips are its own and this
        // method's, leaving the excluded Lazy<T> frames in front of the target.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (StackFrame? Full, StackFrame? Cheap) CaptureBothFromAnExcludedCaller()
        {
            StackWalker.TryGetStackTraceAndFrame(true, out _, out var full);
            StackWalker.TryGetStackTraceAndFrame(false, out _, out var cheap);
            return (full, cheap);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool RecurseUntilTheStackIsNearlyExhausted()
        {
            if (ExecutionStackGuard.HasSufficientStack())
            {
                return RecurseUntilTheStackIsNearlyExhausted();
            }

            return StackWalker.TryGetStackTraceAndFrame(true, out _, out _);
        }
    }
}
