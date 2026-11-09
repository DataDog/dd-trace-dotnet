// <copyright file="WafAttributeRedactionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec.Waf;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafAttributeRedactionTests
{
    [Theory]
    [InlineData("https://api.example.com/files", "https://api.example.com/files")]
    [InlineData("https://api.example.com/files?token=s3cr3t", "https://api.example.com/files")]
    [InlineData("https://api.example.com/files#fragment", "https://api.example.com/files")]
    [InlineData("https://user:pass@api.example.com:8443/files?a=1", "https://api.example.com:8443/files")]
    [InlineData("https://user@api.example.com", "https://api.example.com/")]
    [InlineData("http://api.example.com?key=1", "http://api.example.com/")]
    [InlineData("https://user:p#ss@api.example.com/files", "https://")]
    [InlineData("https://user:p?ss@api.example.com/files", "https://")]
    [InlineData("//api.example.com?next=user@example.net", "//")]
    [InlineData("//api.example.com#user@example.net", "//")]
    [InlineData("https://user:1234?ss@api.example.com/files", "https://")]
    [InlineData("\\\\user:pass@api.example.com/next?a=1", "\\\\api.example.com/next")]
    [InlineData("/login?next=https://user:pass@x.example.com", "/login")]
    [InlineData("https://api.example.com/path/with@sign", "https://api.example.com/path/with@sign")]
    [InlineData("https://[::1]:8080/files?a=1", "https://[::1]:8080/files")]
    [InlineData("//user:pass@api.example.com/next?a=1", "//api.example.com/next")]
    [InlineData("/login?next=/home&token=s3cr3t", "/login")]
    [InlineData(" https://user:1234?ss@api.example.com/files", "https://")]
    [InlineData("\t\n https://user:pass@api.example.com/files?a=1 \r\n", "https://api.example.com/files")]
    [InlineData("ht\ttps://user:pass@api.example.com/fi\nles", "https://api.example.com/files")]
    [InlineData("login#top", "login")]
    [InlineData("", "")]
    public void GivenAUrl_WhenItIsRedacted_ThenUserInfoQueryAndFragmentAreRemoved(string url, string expected)
    {
        WafAttributeRedaction.RemoveUrlSecrets(url).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://user:", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:p?ss", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:pa@ss", 'a', WafConstants.MaxStringLength)]
    [InlineData("//user:", 'a', WafConstants.MaxStringLength)]
    [InlineData(" https://user:", 'a', WafConstants.MaxStringLength)]
    [InlineData("https://user:", (char)0xE4, WafConstants.MaxStringLength * 2 / 3)]
    public void GivenAUrlTruncatedInsideItsAuthority_WhenItIsRedacted_ThenTheAuthorityIsDropped(string prefix, char filler, int length)
    {
        var url = (prefix + new string(filler, length)).Substring(0, length);

        var redacted = WafAttributeRedaction.RemoveUrlSecrets(url);

        redacted.Should().Be(prefix.Substring(0, prefix.IndexOf("//", StringComparison.Ordinal) + 2).TrimStart());
    }

    [Fact]
    public void GivenALongUrlWithAPath_WhenItIsRedacted_ThenTheHostIsKept()
    {
        var url = "https://user:pass@api.example.com/files?token=" + new string('a', WafConstants.MaxStringLength);

        WafAttributeRedaction.RemoveUrlSecrets(url.Substring(0, WafConstants.MaxStringLength)).Should().Be("https://api.example.com/files");
    }

    [Theory]
    [InlineData("https:///user:pass@api.example.com/files")]
    [InlineData("https:/user:pass@api.example.com/files")]
    [InlineData("https:user:pass@api.example.com/files")]
    [InlineData("HTTPS:\\\\\\user:pass@api.example.com/files")]
    [InlineData("http:\t//user:pass@api.example.com/files")]
    [InlineData("///user:pass@api.example.com/files")]
    [InlineData("wss:/user:pass@api.example.com/files")]
    [InlineData("FTP:user:pass@api.example.com/files")]
    public void GivenAUrlWithUnusualSlashes_WhenItIsRedacted_ThenTheUserInfoIsRemoved(string url)
    {
        WafAttributeRedaction.RemoveUrlSecrets(url).Should().NotContain("user").And.NotContain("pass").And.Contain("api.example.com");
    }
}
