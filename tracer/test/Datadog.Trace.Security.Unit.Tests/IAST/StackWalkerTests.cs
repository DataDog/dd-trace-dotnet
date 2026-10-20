// <copyright file="StackWalkerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;
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

        // .NET 8 NativeAOT: the frames have no assembly, so the namespace of their type stands for it.
        [Theory]
        [InlineData("Datadog.Trace.Iast.IastModule.GetScope(String, IntegrationId) + 0x1f", true)]
        [InlineData("System.IO.File.ReadAllText(String) + 0x9", true)]
        [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware.Invoke(HttpContext httpContext) + 0x39c", true)]
        [InlineData("MySqlConnector.MySqlCommand.ExecuteReader() + 0x5", true)]
        [InlineData("Program.<>c.<<Main>$>b__0_1(String) + 0x5", false)]
        [InlineData("SystemOfGame.Player.Run() + 0x1", false)]
        [InlineData("MySqlHelper.Query.Run() + 0x1", false)]
        public void CheckFrameExclusionWithoutAssembly(string frameText, bool outcome)
        {
            StackFrameMethod.TryParse(frameText, out var method).Should().BeTrue();
            method.AssemblyName.Should().BeNull();
            StackWalker.MustSkip(method).Should().Be(outcome);
        }

        // JIT and .NET 9+ NativeAOT: the assembly decides, not the namespace.
        [Theory]
        [InlineData("Datadog.Trace.Iast.IastModule", "Datadog.Trace", true)]
        [InlineData("System.Lazy`1", "System.Private.CoreLib", true)]
        [InlineData("Program+<>c", "App", false)]
        [InlineData("System.Extensions.Helper", "App", false)]
        public void CheckFrameExclusionWithAssembly(string declaringTypeName, string assemblyName, bool outcome)
        {
            StackFrameMethod.TryCreate(declaringTypeName, "Run", assemblyName, out var method).Should().BeTrue();
            StackWalker.MustSkip(method).Should().Be(outcome);
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
