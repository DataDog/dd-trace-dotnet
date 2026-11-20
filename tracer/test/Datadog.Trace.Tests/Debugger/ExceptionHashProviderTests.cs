// <copyright file="ExceptionHashProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ExceptionHashProviderTests
    {
        [Fact]
        public void GetHash_MatchesKnownValue()
        {
            var exceptionId = new ExceptionIdentifier(
                [typeof(InvalidOperationException)],
                [new ParticipatingFrame(new StackFrame(), ParticipatingFrameState.Default)],
                ErrorOriginKind.FirstChanceException);

            // Every tracer build and runtime must produce this value; changing it changes the reported exception_hash.
            ExceptionHashProvider.GetHash(exceptionId).Should().Be("6e17fa0c3387a83d");
        }
    }
}
