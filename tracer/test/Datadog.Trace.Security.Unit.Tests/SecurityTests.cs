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
    public class SecurityTests
    {
        [Fact]
        public void DefaultBehavior()
        {
            var target = new AppSec.Security();
            var action = target.GetBlockingAction(new[] { "wrong" }, null, null);
            action.StatusCode.Should().Be(403);
            action.ResponseContent.Should().Be(SecurityConstants.BlockedJsonTemplate);
            action.ContentType.Should().Be("application/json");
        }

        [Theory]
        [InlineData(BlockingAction.BlockRequestType, null, 200, "auto", 200, "application/json", SecurityConstants.BlockedJsonTemplate)]
        [InlineData(BlockingAction.BlockRequestType, null, 201, "html", 201, "text/html", SecurityConstants.BlockedHtmlTemplate)]
        [InlineData(BlockingAction.RedirectRequestType, "/toto", 302, null, 302, null, null)]
        // if the wrong redirect status code should default to 303
        [InlineData(BlockingAction.RedirectRequestType, "/toto", 404, null, 303, null, null)]
        // if no location default to block request with default template  / 403
        [InlineData(BlockingAction.RedirectRequestType, "", 303, null, 403, "application/json", SecurityConstants.BlockedJsonTemplate)]
        public void CustomActions(string type, string location, int statusCode, string contentType, int expectedStatusCode, string expectedContentType, string expectedContent)
        {
            var target = new AppSec.Security();
            var blockInfo = CreateBlockParameters(type, location, statusCode, contentType);
            var action = target.GetBlockingAction(new[] { "application/json" }, type == BlockingAction.BlockRequestType ? blockInfo : null, type == BlockingAction.RedirectRequestType ? blockInfo : null);
            action.StatusCode.Should().Be(expectedStatusCode);
            action.ResponseContent.Should().Be(expectedContent);
            action.ContentType.Should().Be(expectedContentType);
        }

        [Theory]
        [InlineData(BlockingAction.BlockRequestType, null, 200, "auto", 200, "application/json", SecurityConstants.BlockedJsonTemplate)]
        [InlineData(BlockingAction.BlockRequestType, null, 201, "html", 201, "text/html", SecurityConstants.BlockedHtmlTemplate)]
        [InlineData(BlockingAction.RedirectRequestType, "/toto", 302, null, 302, null, null)]
        public void CustomActionsWithBlockInfo(string type, string location, int statusCode, string contentType, int expectedStatusCode, string expectedContentType, string expectedContent)
        {
            var target = new AppSec.Security();
            var blockInfo = CreateBlockParameters(type, location, statusCode, contentType);

            var action = target.GetBlockingAction(new[] { "application/json" }, type == BlockingAction.BlockRequestType ? blockInfo : null, type == BlockingAction.RedirectRequestType ? blockInfo : null);
            action.StatusCode.Should().Be(expectedStatusCode);
            action.ResponseContent.Should().Be(expectedContent);
            action.ContentType.Should().Be(expectedContentType);
        }

        private static Dictionary<string, object> CreateBlockParameters(string type, string location, int statusCode, string contentType)
        {
            return new Dictionary<string, object>
            {
                { "type", contentType },
                { "status_code", statusCode.ToString() },
                { "location", location },
            };
        }
    }
}
