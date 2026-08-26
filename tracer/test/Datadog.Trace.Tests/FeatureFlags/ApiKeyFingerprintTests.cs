// <copyright file="ApiKeyFingerprintTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.FeatureFlags.Evp;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class ApiKeyFingerprintTests
{
    [Theory]
    [InlineData("", "rijn_RZwTDmWjELXeEmMEb0eIIegKayGGUPNsuJweEPhlXi5")]
    [InlineData("padding-171", "rijn_053ybBRXypQt9AC6UIlqH1YCFYSV1rQl8HCDIcBZs3D")]
    [InlineData("!@#$%^𐍈한€हИ£", "rijn_eFLHeyLxwaiNs2hY16pjkjNjVSHWRgf2rlveKc8YA1K")]
    [InlineData("secret", "rijn_amLaG4Pd6h6t9VtJna81k744P1DYxGHzIJ6ECO3OOMj")]
    [InlineData("system-tests-mock-api-key", "rijn_Fc1Sxm6lPHiKU1IdWeNqpcVZiiW3C2LXJLqQp670sFU")]
    public void Create_MatchesCanonicalVectors(string apiKey, string expected)
    {
        ApiKeyFingerprint.Create(apiKey).Should().Be(expected).And.HaveLength(48);
    }
}
