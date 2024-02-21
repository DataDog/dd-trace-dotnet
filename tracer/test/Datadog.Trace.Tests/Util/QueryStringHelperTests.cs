// <copyright file="QueryStringHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Web;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class QueryStringHelperTests
{
    [Fact]
    public void GivenADangerousQueryString_WhenGetQueryString_HelperAvoidsException()
    {
        var request = new HttpRequest("file", "http://random.com/benchmarks", "data=<script>alert(1)</script>");
        request.ValidateInput();

        try
        {
            _ = request.QueryString;
            Assert.True(false);
        }
        catch (HttpRequestValidationException)
        {
            var request2 = new HttpRequest("file", "http://random.com/benchmarks", "data=<script>alert(1)</script>");
            request2.ValidateInput();
            var queryString = QueryStringHelper.GetQueryString(request2);
            queryString.Should().BeNull();
        }
    }
}
#endif
