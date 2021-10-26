// <copyright file="SerilogLogFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.Formatting;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Parsing;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog
{
    public class SerilogLogFormatterTests
    {
        [Fact]
        public void SerializesEventCorrectly()
        {
            var logEvent = GetLogEvent();
            var log = logEvent.DuckCast<ILogEvent>();

            var formatter = SettingsHelper.GetFormatter();

            var sb = new StringBuilder();
            SerilogLogFormatter.FormatLogEvent(formatter, sb, log);
            var actual = sb.ToString();

            var expected = @"{""@t"":""2021-09-13T10:40:57.0000000Z"",""@m"":""This is a test with a 123"",""@l"":""Debug"",""@x"":""System.InvalidOperationException: Oops, just a test!"",""Value"":123,""OtherProperty"":62,""@i"":""a9a87aee"",""ddsource"":""csharp"",""ddservice"":""MyTestService"",""dd_env"":""integration_tests"",""dd_version"":""1.0.0"",""host"":""some_host""}";
            actual.Should().Be(expected);
        }

        private static LogEvent GetLogEvent()
        {
            new LoggerConfiguration()
               .CreateLogger()
               .BindMessageTemplate(
                    "This is a test with a {Value}",
                    new object[] { 123 },
                    out var messageTemplate,
                    out var boundProperties);

            // simulate properties added via scopes
            boundProperties = boundProperties
               .Concat(new[] { new LogEventProperty("OtherProperty", new ScalarValue(62)) });

            return new LogEvent(
                timestamp: new DateTimeOffset(2021, 09, 13, 10, 40, 57, TimeSpan.Zero),
                LogEventLevel.Debug,
                exception: new InvalidOperationException("Oops, just a test!"),
                messageTemplate: messageTemplate,
                boundProperties);
        }
    }
}
