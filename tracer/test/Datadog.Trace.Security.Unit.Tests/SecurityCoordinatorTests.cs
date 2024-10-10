// <copyright file="SecurityCoordinatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Coordinator;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecurityCoordinatorTests
    {
        [Fact]
        public void DefaultBehavior()
        {
            var target = new AppSec.Security();
            var secCoord = SecurityCoordinator.TryGet(target, new Mock<Span>().Object);
            secCoord.Should().BeNull();
        }
    }
}
