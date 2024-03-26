// <copyright file="DirectSubmissionNLogTargetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETCOREAPP
using System.Collections.Concurrent;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using NLog;
using NLog.Config;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.NLog
{
    public class DirectSubmissionNLogTargetTests
    {
        [Fact]
        public void LoggerEnqueuesLogMessage()
        {
            var sink = new NLogHelper.TestSink();
            var nlogSink = NLogHelper.CreateTarget(sink, DirectSubmissionLogLevel.Debug);
            var targetProxy = (global::NLog.Targets.Target)NLogCommon<LoggingConfiguration>.CreateNLogTargetProxy(nlogSink);

            var level = LogLevel.Error;
            var logEvent = new LogEventInfo(level, nameof(LoggerEnqueuesLogMessage), "This is some value");

            var proxy = NLogHelper.GetLogEventProxy(logEvent);
            nlogSink.Write(proxy);

            sink.Events.Should().ContainSingle();
        }

        [Fact]
        public void ShouldNotEmitLogWhenNotEnabled()
        {
            var sink = new NLogHelper.TestSink();
            var nlogSink = NLogHelper.CreateTarget(sink, DirectSubmissionLogLevel.Warning);
            var targetProxy = (global::NLog.Targets.Target)NLogCommon<LoggingConfiguration>.CreateNLogTargetProxy(nlogSink);

            var level = LogLevel.Info;
            var logEvent = new LogEventInfo(level, nameof(LoggerEnqueuesLogMessage), "This is some value");

            var proxy = NLogHelper.GetLogEventProxy(logEvent);
            nlogSink.Write(proxy);

            sink.Events.Should().BeEmpty();
        }

        [Fact]
        public void LoggerIncludesPropertiesInLog()
        {
            var formatter = LogSettingsHelper.GetFormatter();
            var sink = new NLogHelper.TestSink();
            var target = NLogHelper.CreateTarget(sink, DirectSubmissionLogLevel.Debug);
            var targetProxy = NLogCommon<LoggingConfiguration>.CreateNLogTargetProxy(target);

            var config = new LoggingConfiguration();
            NLogHelper.AddTargetToConfig(config, targetProxy);

            var logFactory = new LogFactory(config);
            var logger = logFactory.GetLogger(nameof(LoggerIncludesPropertiesInLog));

            // We don't currently record NDC/NDLC
#if (NLOG_45 || NLOG_50)
            var messageTemplate = "This is a message with {Value}";
#else
            var messageTemplate = "This is a message with {0}";
#endif
            var mdcKey = "some mdcKey";
            var mdcValue = "some mdcValue";
#if !NLOG_2
            var mdclKey = "some mdclKey";
            var mdclValue = "some mdclValue";
#endif
#if NLOG_50
            var scKey = "some ScopeContextKey";
            var scValue = "some ScopeContextValue";
            IDisposable scProp = null;
#endif
            // var nestedScope = "some nested name";
            // var nestedDictionary = new Dictionary<string, object> { { "nlcKey", 657 } };
            // var dictValues = nestedDictionary.First();
            try
            {
                MappedDiagnosticsContext.Set(mdcKey, mdcValue);
#if !NLOG_2
                MappedDiagnosticsLogicalContext.Set(mdclKey, mdclValue);
#endif
#if NLOG_50
                scProp = ScopeContext.PushProperty(scKey, scValue);
#endif
                logger.Error(messageTemplate, 123);
            }
            finally
            {
                MappedDiagnosticsContext.Remove(mdcKey);
#if !NLOG_2
                MappedDiagnosticsLogicalContext.Remove(mdclKey);
#endif
#if NLOG_50
                scProp.Dispose();
#endif
            }

            var logEvent = sink.Events.Should().ContainSingle().Subject;

            // get the rendered log
            var sb = new StringBuilder();
            logEvent.Format(sb, formatter);
            var log = sb.ToString();

            log.Should()
               .Contain("This is a message with 123")
               .And.Contain(mdcKey)
               .And.Contain(mdcValue)
#if !NLOG_2
               .And.Contain(mdclKey)
               .And.Contain(mdclValue)
#endif
#if NLOG_50
               .And.Contain(scKey)
               .And.Contain(scValue)
#endif
               // .And.Contain(nestedScope)
               // .And.Contain(dictValues.Key)
               // .And.Contain(dictValues.Value.ToString())
               .And.Contain(DirectSubmissionLogLevelExtensions.Error);
        }
    }
}
#endif
