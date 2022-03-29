// <copyright file="DirectLogSubmissionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission
{
    public class DirectLogSubmissionSettingsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("somethingelse.com")]
        public void WhenDirectLogSubmissionUrlIsProvided_UseIt(string ddSite)
        {
            var expected = "http://some_url.com";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.Url, expected },
                { ConfigurationKeys.Site, ddSite },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void WhenUrlIsNotProvided_AndSiteIsProvided_UsesIt(string url)
        {
            var domain = "my-domain.net";
            var expected = "https://http-intake.logs.my-domain.net:443";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.DirectLogSubmission.Url, url },
                { ConfigurationKeys.Site, domain },
            }));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }

        [Fact]
        public void WhenNeitherSiteOrUrlAreProvided_UsesDefault()
        {
            var expected = "https://http-intake.logs.datadoghq.com:443";
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(new()));

            tracerSettings.LogSubmissionSettings.DirectLogSubmissionUrl.Should().Be(expected);
        }
    }
}
