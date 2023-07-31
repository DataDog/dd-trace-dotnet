// <copyright file="SecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection("SecuritySecuentialTests")]
    public class SecurityTests
    {
        [Fact]
        public void DefaultBehavior()
        {
            var target = new AppSec.Security();
            var action = target.GetBlockingAction("block", new[] { "wrong" });
            action.StatusCode.Should().Be(403);
            action.ResponseContent.Should().Be(SecurityConstants.BlockedJsonTemplate);
            action.ContentType.Should().Be("application/json");
        }

        [Theory]
        [InlineData("block_request", null, 200, "auto", 200, "application/json", SecurityConstants.BlockedJsonTemplate)]
        [InlineData("block_request", null, 201, "html", 201, "text/html", SecurityConstants.BlockedHtmlTemplate)]
        [InlineData("redirect_request", "/toto", 302, null, 302, null, null)]
        // if the wrong redirect status code should default to 303
        [InlineData("redirect_request", "/toto", 404, null, 303, null, null)]
        // if no location default to block request with default template  / 403
        [InlineData("redirect_request", "", 303, null, 403, "application/json", SecurityConstants.BlockedJsonTemplate)]
        public void CustomActions(string type, string location, int statusCode, string contentType, int expectedStatusCode, string expectedContentType, string expectedContent)
        {
            var target = CreateTestTarget(type, location, statusCode, contentType);
            var action = target.GetBlockingAction("block", new[] { "application/json" });
            action.StatusCode.Should().Be(expectedStatusCode);
            action.ResponseContent.Should().Be(expectedContent);
            action.ContentType.Should().Be(expectedContentType);
        }

        private static AppSec.Security CreateTestTarget(string type, string location, int statusCode, string contentType)
        {
            var security = new AppSec.Security(actions: new ReadOnlyDictionary<string, Action>(new Dictionary<string, Action> { { "block", new Action { Id = "block", Type = type, Parameters = new Parameter { Location = location, StatusCode = statusCode, Type = contentType } } } }));

            return security;
        }
    }
}
