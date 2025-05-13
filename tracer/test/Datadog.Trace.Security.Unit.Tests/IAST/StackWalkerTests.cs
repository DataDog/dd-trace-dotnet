// <copyright file="StackWalkerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
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
    }
}
