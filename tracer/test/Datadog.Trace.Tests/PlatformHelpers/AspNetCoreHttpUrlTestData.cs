// <copyright file="AspNetCoreHttpUrlTestData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers;

public static class AspNetCoreHttpUrlTestData
{
    public static TheoryData<string, string, string, string> EscapedPaths => new()
    {
        { string.Empty, "/path with spaces", "http://localhost/path%20with%20spaces", "GET /path%20with%20spaces" },
        { "/base path", "/café/東京", "http://localhost/base%20path/caf%C3%A9/%E6%9D%B1%E4%BA%AC", "GET /base%20path/caf%c3%a9/%e6%9d%b1%e4%ba%ac" },
        { string.Empty, "/reserved?#[]", "http://localhost/reserved%3F%23%5B%5D", "GET /reserved%3f%23%5b%5d" },
        { string.Empty, "/already%20escaped", "http://localhost/already%20escaped", "GET /already%20escaped" },
        { "/!$&'()*+,-.:;=@_~", "/AZaz09", "http://localhost/!$&'()*+,-.:;=@_~/AZaz09", "GET /!$&'()*+,-.:;=@_~/azaz09" },
        { string.Empty, "/invalid%2G", "http://localhost/invalid%252G", "GET /invalid%252g" },
    };
}
