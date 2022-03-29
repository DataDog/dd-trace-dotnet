// <copyright file="MockLogsIntakeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Logging.DirectSubmission;
using Datadog.Trace.Tests.Logging.DirectSubmission.Sink;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class MockLogsIntakeTests
    {
        [Fact]
        public void CanDeserializeJson()
        {
            var serialized = "[{\"@t\":\"2021-09-28T14:47:02.6486114Z\",\"@m\":\"[ExcludeMessage]Building pipeline\",\"@i\":\"c5676978\",\"ddsource\":\"csharp\",\"service\":\"LogsInjection.ILogger\",\"host\":\"integration_ilogger_tests\",\"dd.env\":\"integration_tests\",\"dd.version\":\"1.0.0\",\"dd.trace_id\":\"8172771144023044714\",\"dd.span_id\":\"870590545642546651\"}]";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(serialized));

            var logs = MockLogsIntake.Log.DeserializeFromStream(ms);

            logs.Should().NotBeNull();
            var log = logs.Should().ContainSingle().Subject;
            log.Message.Should().Be("[ExcludeMessage]Building pipeline");
            log.EventId.Should().Be("c5676978");
            log.Source.Should().Be("csharp");
            log.Service.Should().Be("LogsInjection.ILogger");
            log.Host.Should().Be("integration_ilogger_tests");
            log.Env.Should().Be("integration_tests");
            log.Version.Should().Be("1.0.0");
            log.TraceId.Should().Be("8172771144023044714");
            log.SpanId.Should().Be("870590545642546651");
        }

        [Fact]
        public void CanDeserializeFormattedLogs()
        {
            var formatter = LogSettingsHelper.GetFormatter();
            var log1 = GetLog(formatter, "This is a {Value}", DirectSubmissionLogLevel.Warning, new Dictionary<string, string> { { "Value", "SomeValue" } });
            var log2 = GetLog(formatter, "This is another template with some other value", DirectSubmissionLogLevel.Information, new Dictionary<string, string> { { "Value", "SomeOtherValue" } });

            var serialized = $"[{log1},{log2}]";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(serialized));

            var logs = MockLogsIntake.Log.DeserializeFromStream(ms);

            logs.Should().NotBeNull();
            logs.Should().HaveCount(2);
        }

        private static string GetLog(
            LogFormatter formatter,
            string message,
            DirectSubmissionLogLevel level,
            Dictionary<string, string> properties)
        {
            var sb = new StringBuilder();

            formatter.FormatLog(
                sb,
                sb, // not used here
                DateTime.UtcNow,
                message,
                null,
                level.GetName(),
                exception: null,
                RenderPropertiesDelegate);
            return sb.ToString();

            LogPropertyRenderingDetails RenderPropertiesDelegate(JsonTextWriter writer, in StringBuilder stringBuilder)
            {
                foreach (var pair in properties)
                {
                    writer.WritePropertyName(pair.Key);
                    writer.WriteValue(pair.Value);
                }

                return new LogPropertyRenderingDetails(false, false, false, false, false, false, message);
            }
        }
    }
}
