// <copyright file="ServiceRemappingHashTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class ServiceRemappingHashTests
{
    [Fact]
    public void Constructor_WithProcessTags_ComputesInitialBase64Value()
    {
        var hash = new ServiceRemappingHash("process:tag,hello:world");

        hash.ContainerTagsHash.Should().BeNull();
        hash.Base64Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithoutProcessTags_LeavesBase64ValueNull()
    {
        var hash = new ServiceRemappingHash(null);

        hash.ContainerTagsHash.Should().BeNull();
        hash.Base64Value.Should().BeNull();
    }

    [Fact]
    public void UpdateContainerTagsHash_WithProcessTags_UpdatesContainerHashAndBase64Value()
    {
        var hash = new ServiceRemappingHash("process:tag,hello:world");
        var initialBase64Value = hash.Base64Value;

        hash.UpdateContainerTagsHash("container0");

        hash.ContainerTagsHash.Should().Be("container0");
        hash.Base64Value.Should().NotBe(initialBase64Value);
    }

    [Fact]
    public void UpdateContainerTagsHash_WithoutProcessTags_UpdatesOnlyContainerHash()
    {
        var hash = new ServiceRemappingHash(null);

        hash.UpdateContainerTagsHash("container0");

        hash.ContainerTagsHash.Should().Be("container0");
        hash.Base64Value.Should().BeNull();
    }

    [Theory]
    [InlineData("svc.auto:service", null, "X2HKTA63-84")]
    [InlineData("svc.auto:service", "container0", "ANVkaLuv_RQ")]
    public void Compute_ReturnsExpectedUrlSafeBase64WithoutPadding(string processTags, string containerTagsHash, string expected)
    {
        var hash = new ServiceRemappingHash(processTags);
        hash.UpdateContainerTagsHash(containerTagsHash);

        hash.Base64Value.Should().Be(expected);
        hash.Base64Value.Should().NotContain("+");
        hash.Base64Value.Should().NotContain("/");
        hash.Base64Value.Should().NotContain("=");
    }
}
