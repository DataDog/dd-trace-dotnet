// <copyright file="HttpSemanticConventionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.OpenTelemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class HttpSemanticConventionsTests
{
    public static TheoryData<string> AllKnownMethods() =>
        new() { "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "QUERY", "TRACE" };

    [Theory]
    // The RFC 9110 methods, plus PATCH and QUERY
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("POST", "POST")]
    [InlineData("PUT", "PUT")]
    [InlineData("QUERY", "QUERY")]
    [InlineData("TRACE", "TRACE")]

    // Known methods are converted to their canonical form
    [InlineData("get", "GET")]
    [InlineData("Post", "POST")]
    [InlineData("pAtCh", "PATCH")]

    // Anything else is _OTHER
    [InlineData("FOO", "_OTHER")]
    [InlineData("GETS", "_OTHER")]
    [InlineData("GE", "_OTHER")]
    [InlineData("_OTHER", "_OTHER")]
    [InlineData(" GET", "_OTHER")]
    [InlineData("", "_OTHER")]
    [InlineData(null, "_OTHER")]
    public void NormalizeRequestMethod_ReturnsKnownMethodOrOther(string httpMethod, string expected)
    {
        HttpSemanticConventions.NormalizeRequestMethod(httpMethod).Should().Be(expected);
    }

    [Theory]
    [InlineData("GET", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("_OTHER", "HTTP")]
    public void GetSpanName_UsesHttpForUnknownMethods(string requestMethod, string expected)
    {
        HttpSemanticConventions.GetSpanName(requestMethod).Should().Be(expected);
    }

    // NormalizeRequestMethod holds the known methods twice: an ordinal switch for the fast path,
    // and a case-insensitive dictionary for the fallback. Exercising every method in both its
    // canonical and its lower-case form covers both, so the two cannot drift apart unnoticed.
    [Theory]
    [MemberData(nameof(AllKnownMethods))]
    public void NormalizeRequestMethod_TakesTheSameDecisionOnBothPaths(string canonicalMethod)
    {
        HttpSemanticConventions.NormalizeRequestMethod(canonicalMethod).Should().Be(canonicalMethod);
        HttpSemanticConventions.NormalizeRequestMethod(canonicalMethod.ToLowerInvariant()).Should().Be(canonicalMethod);
    }
}
