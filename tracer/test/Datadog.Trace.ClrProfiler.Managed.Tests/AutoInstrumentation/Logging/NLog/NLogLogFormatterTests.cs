// <copyright file="NLogLogFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETCOREAPP
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Formatting;
using FluentAssertions;
using NLog;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.NLog
{
    public class NLogLogFormatterTests
    {
        [Fact]
        public void SerializesEventCorrectlyWhenPropertiesAreAvailable()
        {
            var logEvent = GetLogEvent();
            var log = NLogHelper.GetLogEventProxy(logEvent);
            var properties = new Dictionary<string, object>
            {
                { "Value", 123 },
                { "OtherProperty", 62 },
            };

            var wrapper = new LogEntry(log, properties, fallbackProperties: null);

            var formatter = SettingsHelper.GetFormatter();

            var sb = new StringBuilder();
            NLogLogFormatter.FormatLogEvent(formatter, sb, wrapper);
            var actual = sb.ToString();

            var expected = @"{""@t"":""2021-09-13T10:40:57.0000000Z"",""@m"":""This is a test with a 123"",""@l"":""Debug"",""@x"":""System.InvalidOperationException: Oops, just a test!"",""Value"":123,""OtherProperty"":62,""@i"":""e5450052"",""ddsource"":""csharp"",""ddservice"":""MyTestService"",""dd_env"":""integration_tests"",""dd_version"":""1.0.0"",""host"":""some_host""}";
            actual.Should().Be(expected);
        }

        [Fact]
        public void SerializesEventCorrectlyWhenUsingFallbackProperties()
        {
            var logEvent = GetLogEvent();
            var log = NLogHelper.GetLogEventProxy(logEvent);
            var fallback = new Dictionary<object, object>
            {
                { "Value", 123 },
                { "OtherProperty", 62 },
            };

            var wrapper = new LogEntry(log, properties: null, fallback);

            var formatter = SettingsHelper.GetFormatter();

            var sb = new StringBuilder();
            NLogLogFormatter.FormatLogEvent(formatter, sb, wrapper);
            var actual = sb.ToString();

            var expected = @"{""@t"":""2021-09-13T10:40:57.0000000Z"",""@m"":""This is a test with a 123"",""@l"":""Debug"",""@x"":""System.InvalidOperationException: Oops, just a test!"",""Value"":123,""OtherProperty"":62,""@i"":""e5450052"",""ddsource"":""csharp"",""ddservice"":""MyTestService"",""dd_env"":""integration_tests"",""dd_version"":""1.0.0"",""host"":""some_host""}";
            actual.Should().Be(expected);
        }

        private static LogEventInfo GetLogEvent()
        {
            return new LogEventInfo(
                LogLevel.Debug,
                loggerName: nameof(NLogLogFormatterTests),
                message: "This is a test with a {0}",
                formatProvider: CultureInfo.InvariantCulture,
                parameters: new object[] { 123 },
                exception: new InvalidOperationException("Oops, just a test!"))
            {
                TimeStamp = new DateTime(2021, 09, 13, 10, 40, 57, DateTimeKind.Utc),
            };
        }
    }
}
#endif
