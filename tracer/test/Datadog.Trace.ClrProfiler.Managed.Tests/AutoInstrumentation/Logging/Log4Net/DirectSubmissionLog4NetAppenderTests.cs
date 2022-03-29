// <copyright file="DirectSubmissionLog4NetAppenderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Util;
using Xunit;
using Level = log4net.Core.Level;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Log4Net
{
    public class DirectSubmissionLog4NetAppenderTests
    {
        [Fact]
        public void LoggerEnqueuesLogMessage()
        {
            var sink = new Log4NetHelper.TestSink();
            var appender = Log4NetHelper.GetAppender(sink, DirectSubmissionLogLevel.Debug);

            var level = Level.Error;
            var logEvent = new LoggingEvent(
                new LoggingEventData
                {
                    Message = "This is {SomeValue}",
                    LoggerName = nameof(DirectSubmissionLog4NetAppenderTests),
#if LOG4NET_2
                    TimeStampUtc = DateTime.UtcNow,
#else
                    TimeStamp = DateTime.Now,
#endif
                    Level = level,
                    Properties = new PropertiesDictionary { ["SomeValue"] = "someValue!" }
                });

            var proxy = Log4NetHelper.DuckCastLogEvent(logEvent);
            appender.DoAppend(proxy);

            sink.Events.Should().ContainSingle();
        }

        [Fact]
        public void ShouldNotEmitLogWhenNotEnabled()
        {
            var sink = new Log4NetHelper.TestSink();
            var appender = Log4NetHelper.GetAppender(sink, DirectSubmissionLogLevel.Warning);

            var level = Level.Info;
            var logEvent = new LoggingEvent(
                new LoggingEventData
                {
                    Message = "This is {SomeValue}",
                    LoggerName = nameof(DirectSubmissionLog4NetAppenderTests),
#if LOG4NET_2
                    TimeStampUtc = DateTime.UtcNow,
#else
                    TimeStamp = DateTime.Now,
#endif
                    Level = level,
                    Properties = new PropertiesDictionary { ["SomeValue"] = "someValue!" }
                });

            var proxy = Log4NetHelper.DuckCastLogEvent(logEvent);
            appender.DoAppend(proxy);

            sink.Events.Should().BeEmpty();
        }

        [Fact]
        public void LoggerIncludesPropertiesInLog()
        {
            var formatter = LogSettingsHelper.GetFormatter();
            var memoryAppender = new MemoryAppender();
            var sink = new Log4NetHelper.TestSink();
            var appender = Log4NetHelper.GetAppender(sink, DirectSubmissionLogLevel.Debug);
            var appenderProxy = (IAppender)appender.DuckImplement(typeof(IAppender));
            var repository = log4net.LogManager.GetRepository();
            BasicConfigurator.Configure(repository, memoryAppender, appenderProxy);
            var logger = LogManager.GetLogger(typeof(DirectSubmissionLog4NetAppenderTests));

            // Set an ambient property
            var someKey = "some key";
            var someValue = "some Value";
            log4net.ThreadContext.Properties[someKey] = someValue;

            // write the log
            var message = "This is a value";
            logger.Error(message);

            // remove the context before rendering
            log4net.ThreadContext.Properties.Remove(someKey);

            var logEvent = sink.Events.Should().ContainSingle().Subject;

            // get the rendered log
            var sb = new StringBuilder();
            logEvent.Format(sb, formatter);
            var log = sb.ToString();

            log.Should()
               .Contain(message)
               .And.Contain(someKey)
               .And.Contain(someValue)
               .And.Contain(DirectSubmissionLogLevelExtensions.Error);
        }
    }
}
