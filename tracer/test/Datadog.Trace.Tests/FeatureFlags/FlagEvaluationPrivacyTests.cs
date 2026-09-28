// <copyright file="FlagEvaluationPrivacyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationPrivacyTests
{
    [Theory]
    [InlineData("jane.doe@datadoghq.com", "sha256_b4698f9b6d186781fa8dc59e533578fa2d8379a46b1cf6db85cda6aa9c99e51b")]
    [InlineData("user-123", "sha256_fcdec6df4d44dbc637c7c5b58efface52a7f8a88535423430255be0bb89bedd8")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void ProtectedTargetMatchesCrossSdkContract(string? input, string? expected)
    {
        FlagEvaluationPrivacy.TargetingKeyForOutput(input, false).Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidUtf16IsOmittedInsteadOfRepaired(bool consent)
    {
        FlagEvaluationPrivacy.TargetingKeyForOutput("\uD800", consent).Should().BeNull();
        FlagEvaluationPrivacy.TargetingKeyForOutput("prefix\uDC00suffix", consent).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Subject ")]
    [InlineData("é😀")]
    [InlineData("sha256_customer-input")]
    [InlineData(null)]
    public void FullConsentPreservesExactValidText(string? input)
    {
        FlagEvaluationPrivacy.TargetingKeyForOutput(input, true).Should().Be(input);
    }

    [Fact]
    public void ProtectedHashDoesNotNormalizeOrTrustHashLookingInput()
    {
        FlagEvaluationPrivacy.TargetingKeyForOutput(" Subject ", false).Should().NotBe(FlagEvaluationPrivacy.TargetingKeyForOutput("Subject", false));
        FlagEvaluationPrivacy.TargetingKeyForOutput("Subject", false).Should().NotBe(FlagEvaluationPrivacy.TargetingKeyForOutput("subject", false));
        FlagEvaluationPrivacy.TargetingKeyForOutput("é", false).Should().NotBe(FlagEvaluationPrivacy.TargetingKeyForOutput("e\u0301", false));
        FlagEvaluationPrivacy.TargetingKeyForOutput("sha256_customer-input", false).Should().StartWith("sha256_").And.HaveLength(71).And.NotBe("sha256_customer-input");
    }

    [Theory]
    [InlineData("FLAG_NOT_FOUND")]
    [InlineData("INVALID_CONTEXT")]
    [InlineData("PARSE_ERROR")]
    [InlineData("PROVIDER_FATAL")]
    [InlineData("PROVIDER_NOT_READY")]
    [InlineData("TARGETING_KEY_MISSING")]
    [InlineData("TYPE_MISMATCH")]
    [InlineData("GENERAL")]
    [InlineData(null)]
    public void KnownErrorsAndAbsenceArePreserved(string? code)
    {
        FlagEvaluationPrivacy.ErrorCodeForOutput(code).Should().Be(code);
    }

    [Theory]
    [InlineData("private diagnostic")]
    [InlineData("general")]
    [InlineData(" GENERAL ")]
    public void UnknownErrorsAreSanitized(string code)
    {
        FlagEvaluationPrivacy.ErrorCodeForOutput(code).Should().Be("GENERAL");
    }

    [Fact]
    public void EmptyErrorIsAbsentLikeOtherSdks()
    {
        FlagEvaluationPrivacy.ErrorCodeForOutput(string.Empty).Should().BeNull();
    }
}
