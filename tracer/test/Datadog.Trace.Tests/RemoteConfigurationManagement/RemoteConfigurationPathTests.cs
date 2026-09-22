// <copyright file="RemoteConfigurationPathTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.RemoteConfigurationManagement;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement
{
    public class RemoteConfigurationPathTests
    {
        // "Real Values
        [Theory]
        [InlineData("datadog/2/ASM_FEATURES/ASM_FEATURES-base/config", "ASM_FEATURES", "ASM_FEATURES-base")]
        [InlineData("datadog/2/ASM_DD/ASM_DD-base/config", "ASM_DD", "ASM_DD-base")]
        [InlineData("datadog/2/ASM/ASM-base/config", "ASM", "ASM-base")]
        [InlineData("datadog/2/LIVE_DEBUGGING/LIVE_DEBUGGING-second/config", "LIVE_DEBUGGING", "LIVE_DEBUGGING-second")]
        [InlineData("datadog/2/ASM_DATA/ASM_DATA-rules/config", "ASM_DATA", "ASM_DATA-rules")]
        [InlineData("datadog/2/APM_TRACING/my-config-id/config", "APM_TRACING", "my-config-id")]
        [InlineData("datadog/2/AGENT_CONFIG/flare-123/config", "AGENT_CONFIG", "flare-123")]
        [InlineData("datadog/2/AGENT_TASK/task-abc/config", "AGENT_TASK", "task-abc")]
        // Different org numbers after "datadog/"
        [InlineData("datadog/0/PROD/id/file", "PROD", "id")]
        [InlineData("datadog/1/PROD/id/file", "PROD", "id")]
        [InlineData("datadog/2/PROD/id/file", "PROD", "id")]
        [InlineData("datadog/00/PROD/id/file", "PROD", "id")]
        [InlineData("datadog/123/PROD/id/file", "PROD", "id")]
        [InlineData("datadog/999999/PROD/id/file", "PROD", "id")]
        // The filename (last segment) can be anything — it's not captured
        [InlineData("datadog/2/PROD/myid/config", "PROD", "myid")]
        [InlineData("datadog/2/PROD/myid/testname", "PROD", "myid")]
        [InlineData("datadog/2/PROD/myid/anything-here", "PROD", "myid")]
        [InlineData("datadog/2/PROD/myid/123", "PROD", "myid")]
        // Valid employee paths: "employee/{product}/{id}/{filename}"
        [InlineData("employee/john/doe/smith", "john", "doe")]
        [InlineData("employee/1/2/3", "1", "2")]
        [InlineData("employee/4/3/2", "4", "3")]
        [InlineData("employee/1/other-ID/3", "1", "other-ID")]
        [InlineData("employee/PRODUCT/some-config/file", "PRODUCT", "some-config")]
        // Edge cases for segment content
        [InlineData("datadog/2/a/b/c", "a", "b")]                // single-char segments
        [InlineData("datadog/2/A-B_C/x-y_z/f", "A-B_C", "x-y_z")] // hyphens and underscores
        [InlineData("datadog/2/PROD/id.with.dots/config", "PROD", "id.with.dots")] // dots in id
        public void FromPath_ExtractsProductAndId(string path, string expectedProduct, string expectedId)
        {
            var result = RemoteConfigurationPath.FromPath(path);

            result.Path.Should().BeSameAs(path);
            result.Product.Should().Be(expectedProduct);
            result.Id.Should().Be(expectedId);
        }

        [Theory]
        [InlineData("datadog/2/ASM_FEATURES/ASM_FEATURES-base/config")]
        [InlineData("employee/1/other-ID/3")]
        public void FromPath_SamePath_ProducesEqualInstances(string path)
        {
            var a = RemoteConfigurationPath.FromPath(path);
            var b = RemoteConfigurationPath.FromPath(path);

            a.Should().Be(b);
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void FromPath_DifferentPaths_ProducesUnequalInstances()
        {
            var a = RemoteConfigurationPath.FromPath("datadog/2/ASM/config-1/config");
            var b = RemoteConfigurationPath.FromPath("datadog/2/ASM/config-2/config");

            a.Should().NotBe(b);
        }

        [Theory]
        [InlineData("")]                                          // empty
        [InlineData("datadog")]                                   // only prefix
        [InlineData("datadog/2")]                                 // missing product, id, filename
        [InlineData("datadog/2/PROD")]                            // missing id and filename
        [InlineData("datadog/2/PROD/id")]                         // missing filename (only 3 segments)
        [InlineData("datadog/2/PROD/id/name/extra")]              // too many segments (5 after prefix)
        [InlineData("datadog/abc/PROD/id/config")]                // non-digit org number
        [InlineData("datadog//PROD/id/config")]                   // empty org number
        [InlineData("other/2/PROD/id/config")]                    // wrong prefix
        [InlineData("DATADOG/2/PROD/id/config")]                  // case-sensitive prefix
        [InlineData("employee")]                                  // employee with no segments
        [InlineData("employee/a")]                                // employee missing id and filename
        [InlineData("employee/a/b")]                              // employee missing filename
        [InlineData("employee/a/b/c/d")]                          // employee too many segments
        [InlineData("/datadog/2/PROD/id/config")]                 // leading slash
        [InlineData("datadog/2/PROD/id/config/")]                 // trailing slash
        public void FromPath_InvalidPath_Throws(string path)
        {
            var act = () => RemoteConfigurationPath.FromPath(path);

            act.Should().Throw<Exception>();
        }
    }
}
