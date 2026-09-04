// <copyright file="Md5HelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class Md5HelperTests
{
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb924")]
    [InlineData("test", "9f86d081884c7d659a2feaa0c55ad015")]
    public void ComputeMd5HashUsesTruncatedSha256ForFips(string input, string expected)
    {
        var hash = Md5Helper.ComputeMd5Hash(input, useFipsCompliantAlgorithm: true);

        HexString.ToHexString(hash).Should().Be(expected);
    }
}
#endif
