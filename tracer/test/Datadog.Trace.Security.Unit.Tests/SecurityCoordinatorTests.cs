// <copyright file="SecurityCoordinatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class SecurityCoordinatorTests
{
    [Theory]
    [InlineData("key1", "value1", "key2", "value2", 2)]
    [InlineData(null, "value1", "key2", "value2", 2)]
    [InlineData("key1", null, "key2", "value2", 2)]
    [InlineData(null, null, "key2", "value2", 2)]
    [InlineData("key1", "value1", "key1", "value1", 2)]
    [InlineData(null, null, null, null, 0)]
    public void GivenASecurityCoordinator_WhenGetBody_ThenBodyIsRetrieved(string key1, string value1, string key2, string value2, int count)
    {
        NameValueCollection form = new NameValueCollection()
        {
            { key1, value1 },
            { key2, value2 },
        };

        HttpRequest httpRequest = new HttpRequest(string.Empty, "http://localhost", string.Empty);
        // var formField = typeof(HttpRequest).GetField("_form", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // formField.SetValue(httpRequest, form);
        var readOnlyField = typeof(NameObjectCollectionBase).GetField("_readOnly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        readOnlyField.SetValue(httpRequest.Form, false);
        httpRequest.Form.Add(form);
        var context = new HttpContext(httpRequest, new HttpResponse(new Mock<TextWriter>().Object));
        var securityCoordinator = new SecurityCoordinator(null, context, new Span(new SpanContext(0, 0), DateTimeOffset.MinValue));
        var body = securityCoordinator.GetBodyFromRequest();
        body.Count().Should().Be(count);
    }
}

#endif
