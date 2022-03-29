// <copyright file="RegistryServiceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Runner.Checks.Windows;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests
{
    public class RegistryServiceTests
    {
        [Fact]
        public void DontCrash()
        {
            var result = new RegistryService().GetLocalMachineValueNames("DummyKey");

            result.Should().BeEmpty();
        }
    }
}
