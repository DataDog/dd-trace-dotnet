// <copyright file="LogFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Tests.PlatformHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Logging.DirectSubmission.Formatting
{
    public class LogFormatterTests : IDisposable
    {
        private const string Host = "the_host";
        private const string Source = "csharp";
        private const string Service = "TestService";
        private const string Env = "integrationTests";
        private const string Version = "1.0.0";

        private static readonly NameValueCollection Defaults = new()
        {
            { ConfigurationKeys.ApiKey, "some_value" },
            { ConfigurationKeys.DirectLogSubmission.Host, Host },
            { ConfigurationKeys.DirectLogSubmission.Source, Source },
            { ConfigurationKeys.DirectLogSubmission.Url, "http://localhost" },
            { ConfigurationKeys.DirectLogSubmission.MinimumLevel, "debug" },
            { ConfigurationKeys.DirectLogSubmission.GlobalTags, "Key1:Value1,Key2:Value2" },
            { ConfigurationKeys.DirectLogSubmission.EnabledIntegrations, nameof(IntegrationId.ILogger) },
            { ConfigurationKeys.DirectLogSubmission.BatchSizeLimit, "100" },
            { ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds, "2" },
            { ConfigurationKeys.DirectLogSubmission.QueueSizeLimit, "1000" }
        };

        private readonly JsonTextWriter _writer;
        private readonly StringBuilder _sb;
        private readonly ImmutableTracerSettings _tracerSettings;
        private readonly ImmutableDirectLogSubmissionSettings _directLogSettings;

        public LogFormatterTests()
        {
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(Defaults));
            _tracerSettings = new ImmutableTracerSettings(tracerSettings);
            _directLogSettings = ImmutableDirectLogSubmissionSettings.Create(tracerSettings);
            _directLogSettings.IsEnabled.Should().BeTrue();

            _sb = new StringBuilder();
            _writer = LogFormatter.GetJsonWriter(_sb);
        }

        public void Dispose()
        {
            ((IDisposable)_writer)?.Dispose();
        }

        [Theory]
        [InlineData("name", "\"name\":")]
        [InlineData("@name", "\"@@name\":")]
        [InlineData("@", "\"@@\":")]
        public void EscapesPropertyNames(string property, string expected)
        {
            LogFormatter.WritePropertyName(_writer, property);
            _sb.ToString().Should().Be(expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("some string", "\"some string\"")]
        [InlineData(1234, "1234")]
        [InlineData(1234L, "1234")]
        [InlineData(1234UL, "1234")]
        [InlineData(123.50D, "123.5")]
        [InlineData(123.50F, "123.5")]
        [InlineData('c', "\"c\"")]
        public void WritesValueCorrectly(object value, string expected)
        {
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be(expected);
        }

        [Fact]
        public void WritesDecimalValueCorrectly()
        {
            var value = 123.5M;
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("123.5");
        }

        [Fact]
        public void WritesDateValueCorrectly()
        {
            var value = new DateTime(year: 2020, month: 3, day: 7, hour: 11, minute: 23, second: 26, millisecond: 500, DateTimeKind.Utc);
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("\"2020-03-07T11:23:26.5Z\"");
        }

        [Fact]
        public void WritesDateTimeOffsetValueCorrectly()
        {
            var value = new DateTimeOffset(year: 2020, month: 3, day: 7, hour: 11, minute: 23, second: 26, millisecond: 500, offset: TimeSpan.Zero);
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("\"2020-03-07T11:23:26.5+00:00\"");
        }

        [Fact]
        public void WritesTimespanCorrectly()
        {
            var value = TimeSpan.FromSeconds(100);
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("\"00:01:40\"");
        }

        [Fact]
        public void WritesFormattableObjectCorrectly()
        {
            var value = new FormattableObject { SomeValue = "testing!" };
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("\"SomeValue = testing!\"");
        }

        [Fact]
        public void WritesObjectCorrectly()
        {
            var value = new TestObject { SomeValue = "testing!" };
            LogFormatter.WriteValue(_writer, value);
            _sb.ToString().Should().Be("\"Datadog.Trace.Tests.Logging.DirectSubmission.Formatting.LogFormatterTests+TestObject\"");
        }

        [Theory]
        [MemberData(nameof(TestData.AllOptions), MemberType = typeof(TestData))]
        public void WritesLogFormatCorrectly(
            bool hasRenderedSource,
            bool hasRenderedService,
            bool hasRenderedHost,
            bool hasRenderedTags,
            bool hasRenderedEnv,
            bool hasRenderedVersion,
            bool isInAas)
        {
            var timestamp = new DateTime(year: 2020, month: 3, day: 7, hour: 11, minute: 23, second: 26, millisecond: 500, DateTimeKind.Utc);
            var sb = new StringBuilder();
            var state = new TestObject();
            var message = "Some message";
            var logLevel = "Info";
            var aasSettings = isInAas ? GetAasSettings() : null;

            var formatter = new LogFormatter(
                _tracerSettings,
                _directLogSettings,
                aasSettings: aasSettings,
                serviceName: Service,
                env: Env,
                version: Version,
                gitMetadataTagsProvider: new NullGitMetadataProvider());

            formatter.FormatLog(sb, state, timestamp, message, eventId: null, logLevel, exception: null, RenderProperties);

            var log = sb.ToString();

            log
               .Should()
               .Contain(timestamp.ToString("o"))
               .And.Contain(message)
               .And.Contain(logLevel);

            using var scope = new AssertionScope();
            HasExpectedValue(log, !hasRenderedSource, $"\"ddsource\":\"{Source}\"");
            HasExpectedValue(log, !hasRenderedService, $"\"service\":\"{Service}\"");
            HasExpectedValue(log, !hasRenderedHost, $"\"host\":\"{Host}\"");

            (string Key, string Value)[] expectedTags = (isInAas, hasRenderedTags) switch
            {
                (_, true) => null, // if the log itself adds this, we don't add the global tags currently
                (true, false) => [("aas.resource.id", aasSettings!.ResourceId), ("Key1", "Value1"), ("Key2", "Value2")],
                (false, false) => [("Key1", "Value1"), ("Key2", "Value2")],
            };

            HasExpectedTags(log, expectedTags);
            HasExpectedValue(log, !hasRenderedEnv, $"\"dd_env\":\"{Env}\"");
            HasExpectedValue(log, !hasRenderedVersion, $"\"dd_version\":\"{Version}\"");

            LogPropertyRenderingDetails RenderProperties(JsonTextWriter jsonTextWriter, in TestObject o) =>
                new LogPropertyRenderingDetails(
                    hasRenderedSource: hasRenderedSource,
                    hasRenderedService: hasRenderedService,
                    hasRenderedHost: hasRenderedHost,
                    hasRenderedTags: hasRenderedTags,
                    hasRenderedEnv: hasRenderedEnv,
                    hasRenderedVersion: hasRenderedVersion,
                    messageTemplate: null);

            static List<(string Key, string Value)> ExtractTags(string log)
            {
                var match = Regex.Match(log, "\"ddtags\":\"(.*)\"");

                if (!match.Success)
                {
                    return null;
                }

                var tags = match.Groups[1].Value;

                var result = new List<(string Key, string Value)>();

                foreach (var item in tags.Split(','))
                {
                    var values = item.Split(':');
                    result.Add((values[0], values[1]));
                }

                return result;
            }

            static void HasExpectedTags(string log, (string Key, string Value)[] expectedTags)
            {
                var actualTags = ExtractTags(log);

                if (expectedTags == null)
                {
                    actualTags.Should().BeNull();
                }
                else
                {
                    actualTags.Should().BeEquivalentTo(expectedTags);
                }
            }

            static void HasExpectedValue(string log, bool shouldContain, string value)
            {
                if (shouldContain)
                {
                    log.Should().Contain(value);
                }
                else
                {
                    log.Should().NotContain(value);
                }
            }

            ImmutableAzureAppServiceSettings GetAasSettings()
            {
                return new ImmutableAzureAppServiceSettings(
                    AzureAppServiceHelper.GetRequiredAasConfigurationValues(
                        subscriptionId: "8c500027-5f00-400e-8f00-60000000000f",
                        deploymentId: "AzureExampleSiteName",
                        planResourceGroup: "apm-dotnet",
                        siteResourceGroup: "apm-dotnet-site-resource-group"),
                    NullConfigurationTelemetry.Instance);
            }
        }

        private class TestData
        {
            private static readonly bool[] Options = { true, false };

            public static IEnumerable<object[]> AllOptions
                => from src in Options
                   from service in Options
                   from host in Options
                   from tags in Options
                   from env in Options
                   from version in Options
                   from aas in Options
                   select new object[] { src, service, host, tags, env, version, aas };
        }

        private class FormattableObject : IFormattable
        {
            public string SomeValue { get; set; }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                return "SomeValue = " + SomeValue;
            }
        }

        private class TestObject
        {
            public string SomeValue { get; set; }
        }
    }
}
