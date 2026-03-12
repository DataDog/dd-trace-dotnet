// <copyright file="HttpRequestUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util.Http;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Http;

public class HttpRequestUtilsTests
{
    [SkippableTheory]
    // Basic common cases - no query string manager
    [InlineData("http://localhost/path", false, false, "http://localhost/path")]
    [InlineData("https://example.com/api/users", false, false, "https://example.com/api/users")]
    [InlineData("http://example.org:8080/test", false, false, "http://example.org:8080/test")]

    // Default ports are not included in output (80 for http, 443 for https)
    [InlineData("http://localhost:80/path", false, false, "http://localhost/path")]
    [InlineData("https://localhost:443/path", false, false, "https://localhost/path")]
    [InlineData("http://localhost:8080/path", false, false, "http://localhost:8080/path")]

    // Query strings - without query manager (query string is removed)
    [InlineData("http://localhost/path?key=value", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key=value&foo=bar", false, false, "http://localhost/path")]
    [InlineData("http://localhost/api/users?id=123&name=test", false, false, "http://localhost/api/users")]
    [InlineData("http://localhost/path?", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key", false, false, "http://localhost/path")]

    // Query strings - with query manager (query string is included, sensitive params obfuscated)
    [InlineData("http://localhost/path?key=value", true, false, "http://localhost/path?key=value")]
    [InlineData("http://localhost/path?key=value&foo=bar", true, false, "http://localhost/path?key=value&foo=bar")]
    [InlineData("http://localhost/api/users?id=123", true, false, "http://localhost/api/users?id=123")]
    [InlineData("http://localhost/path?", true, false, "http://localhost/path?")]
    [InlineData("http://localhost/path?key", true, false, "http://localhost/path?key")]

    // Fragments are removed (by Uri class)
    [InlineData("http://localhost/path#section", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path#section", true, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key=value#section", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key=value#section", true, false, "http://localhost/path?key=value")]

    // With dangerous create. the fragment isn't removed
    [InlineData("http://localhost/path#section", false, true, "http://localhost/path#section")]
    [InlineData("http://localhost/path#section", true, true, "http://localhost/path#section")]
    [InlineData("http://localhost/path?key=value#section", false, true, "http://localhost/path")]
    [InlineData("http://localhost/path?key=value#section", true, true, "http://localhost/path?key=value#section")]

    // User information is removed (by Uri class)
    [InlineData("http://user:pass@localhost/path", false, false, "http://localhost/path")]
    [InlineData("http://user@localhost/path", false, false, "http://localhost/path")]
    [InlineData("http://user:pass@localhost/path?q=1", true, false, "http://localhost/path?q=1")]

    // Different schemes
    [InlineData("https://localhost/path", false, false, "https://localhost/path")]
    [InlineData("ftp://example.org/files", false, false, "ftp://example.org/files")]
    [InlineData("ws://localhost:3000/socket", false, false, "ws://localhost:3000/socket")]
    [InlineData("wss://localhost:3000/socket", false, false, "wss://localhost:3000/socket")]

    // Empty and root paths
    [InlineData("http://localhost", false, false, "http://localhost/")]
    [InlineData("http://localhost", false, true, "http://localhost")] // dangerous doesn't canonicalise
    [InlineData("http://localhost/", false, false, "http://localhost/")]
    [InlineData("http://localhost?query=value", false, false, "http://localhost/")]
    [InlineData("http://localhost?query=value", true, false, "http://localhost/?query=value")]
    [InlineData("http://localhost?query=value", true, true, "http://localhost?query=value")]

    // Different ports
    [InlineData("http://localhost:3000/path", false, false, "http://localhost:3000/path")]
    [InlineData("http://localhost:5000/path", false, false, "http://localhost:5000/path")]
    [InlineData("https://localhost:8443/path", false, false, "https://localhost:8443/path")]
    [InlineData("http://localhost:3000/api?key=value", true, false, "http://localhost:3000/api?key=value")]

    // IPv4 addresses
    [InlineData("http://127.0.0.1/path", false, false, "http://127.0.0.1/path")]
    [InlineData("http://192.168.1.1:8080/path", false, false, "http://192.168.1.1:8080/path")]
    [InlineData("http://10.0.0.1/api?test=1", true, false, "http://10.0.0.1/api?test=1")]

    // IPv6 addresses
    [InlineData("http://[::1]/path", false, false, "http://[::1]/path")]
    [InlineData("http://[::1]:8080/path", false, false, "http://[::1]:8080/path")]
    [InlineData("http://[2001:db8::1]/path", false, false, "http://[2001:db8::1]/path")]
    [InlineData("http://[::1]/path?q=1", true, false, "http://[::1]/path?q=1")]

    // Trailing slashes
    [InlineData("http://localhost/path/", false, false, "http://localhost/path/")]
    [InlineData("http://localhost/api/users/", false, false, "http://localhost/api/users/")]
    [InlineData("http://localhost/path/?query=1", true, false, "http://localhost/path/?query=1")]

    // URL-encoded characters (preserved by Uri, unless dangerous create)
    [InlineData("http://localhost/path with spaces", false, false, "http://localhost/path%20with%20spaces")]
    [InlineData("http://localhost/path with spaces", false, true, "http://localhost/path with spaces")]
    [InlineData("http://localhost/path%20with%20spaces", false, false, "http://localhost/path%20with%20spaces")]
    [InlineData("http://localhost/path%20encoded?q=1", true, false, "http://localhost/path%20encoded?q=1")]

    // Special characters in path (automatically escaped by Uri, unless dangerous)
    [InlineData("http://localhost/path<>", false, false, "http://localhost/path%3C%3E")]
    [InlineData("http://localhost/path<>", false, true, "http://localhost/path<>")]
    [InlineData("http://localhost/path\"quotes\"", false, false, "http://localhost/path%22quotes%22")]
    [InlineData("http://localhost/path\"quotes\"", false, true, "http://localhost/path\"quotes\"")]

    // Multiple slashes in path
    [InlineData("http://localhost//double//slash", false, false, "http://localhost//double//slash")]
    [InlineData("http://localhost//path?q=1", true, false, "http://localhost//path?q=1")]

    // Dot segments (normalized by Uri constructor, unless dangerous)
    [InlineData("http://localhost/a/./b", false, false, "http://localhost/a/b")]
    [InlineData("http://localhost/a/../b", false, false, "http://localhost/b")]
    [InlineData("http://localhost/a/./b?q=1", true, false, "http://localhost/a/b?q=1")]
    [InlineData("http://localhost/a/./b", false, true, "http://localhost/a/./b")]
    [InlineData("http://localhost/a/../b", false, true, "http://localhost/a/../b")]
    [InlineData("http://localhost/a/./b?q=1", true, true, "http://localhost/a/./b?q=1")]

    // Mixed case hosts (normalized to lowercase by Uri)
    [InlineData("http://LocalHost/path", false, false, "http://localhost/path")]
    [InlineData("http://EXAMPLE.COM/path", false, false, "http://example.com/path")]
    [InlineData("http://LocalHost/path?q=1", true, false, "http://localhost/path?q=1")]

    // Complex paths
    [InlineData("http://localhost/api/v1/users/123", false, false, "http://localhost/api/v1/users/123")]
    [InlineData("http://localhost/very/long/path/with/many/segments", false, false, "http://localhost/very/long/path/with/many/segments")]
    [InlineData("http://localhost/api/v2/products?sort=name&order=asc", true, false, "http://localhost/api/v2/products?sort=name&order=asc")]

    // Real-world examples
    [InlineData("https://api.github.com/repos/DataDog/dd-trace-dotnet", false, false, "https://api.github.com/repos/DataDog/dd-trace-dotnet")]
    [InlineData("https://api.example.com/v1/search?q=test&limit=10", false, false, "https://api.example.com/v1/search")]
    [InlineData("https://api.example.com/v1/search?q=test&limit=10", true, false, "https://api.example.com/v1/search?q=test&limit=10")]

    // Edge case: Query string with special characters
    [InlineData("http://localhost/path?key=value&special=a%20b", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key=value&special=a%20b", true, false, "http://localhost/path?key=value&special=a%20b")]

    // Edge case: Query string with = but no value
    [InlineData("http://localhost/path?key=", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?key=", true, false, "http://localhost/path?key=")]

    // Edge case: Multiple question marks (only first is treated as query separator)
    [InlineData("http://localhost/path?q1=a?b", false, false, "http://localhost/path")]
    [InlineData("http://localhost/path?q1=a?b", true, false, "http://localhost/path?q1=a?b")]

    // Edge case: Very long query strings (gets truncated when QueryStringManager is used)
    [InlineData("http://localhost/api?param1=value1&param2=value2&param3=value3&param4=value4&param5=value5", false, false, "http://localhost/api")]
    [InlineData("http://localhost/api?param1=value1&param2=value2&param3=value3&param4=value4&param5=value5", true, false, "http://localhost/api?param1=value1&param2=value2&param3=value3&param4=")]

    // Edge case: Query string with encoded characters
    [InlineData("http://localhost/search?q=hello%20world&lang=en", false, false, "http://localhost/search")]
    [InlineData("http://localhost/search?q=hello%20world&lang=en", true, false, "http://localhost/search?q=hello%20world&lang=en")]

    // Edge case: Paths with query-like strings (not actual queries)
    [InlineData("http://localhost/file.html?notquery", false, false, "http://localhost/file.html")]
    [InlineData("http://localhost/file.html?notquery", true, false, "http://localhost/file.html?notquery")]

    // Non-standard ports for common schemes
    [InlineData("http://localhost:443/path", false, false, "http://localhost:443/path")]
    [InlineData("https://localhost:80/path", false, false, "https://localhost:80/path")]

    // FTP default port (21)
    [InlineData("ftp://example.org:21/files", false, false, "ftp://example.org/files")]
    [InlineData("ftp://example.org:2121/files", false, false, "ftp://example.org:2121/files")]

    // Sensitive parameters that get redacted (entire query string from sensitive param onwards becomes ?<redacted>)
    [InlineData("http://localhost/login?password=secret123", true, false, "http://localhost/login?<redacted>")]
    [InlineData("http://localhost/api?api_key=abc123", true, false, "http://localhost/api?<redacted>")]
    [InlineData("http://localhost/auth?token=xyz789", true, false, "http://localhost/auth?<redacted>")]
    [InlineData("http://localhost/api?access_key=mykey", true, false, "http://localhost/api?<redacted>")]
    [InlineData("http://localhost/api?secret=mysecret", true, false, "http://localhost/api?<redacted>")]
    [InlineData("http://localhost/auth?pwd=mypassword", true, false, "http://localhost/auth?<redacted>")]
    [InlineData("http://localhost/api?Authorization=Bearer%20abc123", true, false, "http://localhost/api?<redacted>")]

    // Mixed sensitive and non-sensitive parameters (sensitive values get replaced, rest preserved)
    [InlineData("http://localhost/api?user=john&password=secret", true, false, "http://localhost/api?user=john&<redacted>")]
    [InlineData("http://localhost/api?id=123&api_key=secret&sort=asc", true, false, "http://localhost/api?id=123&<redacted>&sort=asc")]

    // Long query strings that get truncated (maxSizeBeforeObfuscation=50) then obfuscated if needed
    [InlineData("http://localhost/api?param1=value1&param2=value2&param3=value3&param4=value4", true, false, "http://localhost/api?param1=value1&param2=value2&param3=value3&param4=")]
    [InlineData("http://localhost/search?query=this_is_a_very_long_search_query_that_exceeds_fifty_characters", true, false, "http://localhost/search?query=this_is_a_very_long_search_query_that_excee")]
    [InlineData("http://localhost/api?short=val", true, false, "http://localhost/api?short=val")]
    [InlineData("http://localhost/login?user=john&password=verylongsecretpasswordthatexceedsfiftycharacterslimit", true, false, "http://localhost/login?user=john&<redacted>")]
    public void GetUrl_ShouldFormatCorrectly(string url, bool useQueryManager, bool useDangerousCreate, string expected)
    {
#if NET6_0_OR_GREATER
        var uri = useDangerousCreate ? new Uri(url, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true }) : new Uri(url);
#else
        if (useDangerousCreate)
        {
            throw new SkipException();
        }

        var uri = new Uri(url);
#endif
        var queryStringManager = useQueryManager
            ? new QueryStringManager(reportQueryString: true, timeout: 30_000, maxSizeBeforeObfuscation: 50, pattern: TracerSettingsConstants.DefaultObfuscationQueryStringRegex)
            : null;

        var result = HttpRequestUtils.GetUrl(uri, queryStringManager);
        result.Should().Be(expected);
    }
}
