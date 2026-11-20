// <copyright file="Sha256HelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class Sha256HelperTests
{
    [Theory]
    // Frozen reference vectors (lowercase hex, matching the cross-SDK FFE subject hashing).
    [InlineData("user-123", "fcdec6df4d44dbc637c7c5b58efface52a7f8a88535423430255be0bb89bedd8")]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void ComputeHashAsHexString_MatchesKnownVectors(string input, string expected)
    {
        Sha256Helper.ComputeHashAsHexString(input).Should().Be(expected);
    }
}
