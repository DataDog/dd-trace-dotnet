// <copyright file="Log4NetLogFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using log4net.Core;
using log4net.Util;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Log4Net
{
    public class Log4NetLogFormatterTests
    {
        [Fact]
        public void SerializesEventCorrectly()
        {
            var logEvent = GetLogEvent();
            var formatter = LogSettingsHelper.GetFormatter();
            var sb = new StringBuilder();
#if LOG4NET_2
            var log = logEvent.DuckCast<ILoggingEventDuck>();
            Log4NetLogFormatter.FormatLogEvent(formatter, sb, log, log.TimeStampUtc);
#else
            var log = logEvent.DuckCast<ILoggingEventLegacyDuck>();
            Log4NetLogFormatter.FormatLogEvent(formatter, sb, log, log.TimeStamp);
#endif
            var actual = sb.ToString();

            var expected = @"{""@t"":""2021-09-13T10:40:57.0000000Z"",""@m"":""This is a test with a 123"",""@l"":""Debug"",""@x"":""System.InvalidOperationException: Oops, just a test!"",""Value"":123,""@i"":""aad9c020"",""ddsource"":""csharp"",""service"":""MyTestService"",""dd_env"":""integration_tests"",""dd_version"":""1.0.0"",""host"":""some_host""}";
            actual.Should().Be(expected);
        }

        private static LoggingEvent GetLogEvent()
        {
            var loggingEvent = new LoggingEvent(
                typeof(Log4NetLogFormatterTests),
                repository: null,
                loggerName: "Something",
                level: Level.Debug,
                message: "This is a test with a 123",
                exception: new InvalidOperationException("Oops, just a test!"));
            loggingEvent.Properties["Value"] = 123;

            // Yes, this is very annoying
            var loggingDate = new DateTimeOffset(2021, 09, 13, 10, 40, 57, TimeSpan.Zero).UtcDateTime;
            var fieldInfo = typeof(LoggingEvent)
               .GetField("m_data", BindingFlags.Instance | BindingFlags.NonPublic);
            object boxed = fieldInfo!.GetValue(loggingEvent);

#if LOG4NET_2
            typeof(LoggingEventData).GetProperty("TimeStampUtc")!
                                    .SetValue(boxed, loggingDate);
#else
            typeof(LoggingEventData).GetField("TimeStamp")!
                                    .SetValue(boxed, loggingDate);
#endif

            fieldInfo.SetValue(loggingEvent, boxed);

#if LOG4NET_2
            Assert.Equal(loggingDate, loggingEvent.TimeStampUtc);
#else
            Assert.Equal(loggingDate, loggingEvent.TimeStamp);
#endif
            return loggingEvent;
        }
    }
}
