// <copyright file="NLogHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETCOREAPP
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using Datadog.Trace.TestHelpers;
using NLog;
using NLog.Config;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.NLog
{
    internal static class NLogHelper
    {
#if NLOG_50
        public static DirectSubmissionNLogV5Target CreateTarget(IDatadogSink sink, DirectSubmissionLogLevel minimumLevel)
            => new(sink, minimumLevel, LogSettingsHelper.GetFormatter());

        public static ILogEventInfoProxy GetLogEventProxy(LogEventInfo logEvent)
            => logEvent.DuckCast<ILogEventInfoProxy>();

        public static void AddTargetToConfig(LoggingConfiguration config, object targetProxy)
            => NLogCommon<LoggingConfiguration>.AddDatadogTargetNLog50(config, targetProxy);
#elif NLOG_45
        public static DirectSubmissionNLogTarget CreateTarget(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel minimumLevel)
            => new(sink, minimumLevel, LogSettingsHelper.GetFormatter());

        public static ILogEventInfoProxy GetLogEventProxy(LogEventInfo logEvent)
            => logEvent.DuckCast<ILogEventInfoProxy>();

        public static void AddTargetToConfig(LoggingConfiguration config, object targetProxy)
            => NLogCommon<LoggingConfiguration>.AddDatadogTargetNLog45(config, targetProxy);
#else
        public static DirectSubmissionNLogLegacyTarget CreateTarget(IDatadogSink sink, DirectSubmissionLogLevel minimumLevel)
            => new(sink, minimumLevel, SettingsHelper.GetFormatter());

        public static LogEventInfoLegacyProxy GetLogEventProxy(LogEventInfo logEvent)
            => logEvent.DuckCast<LogEventInfoLegacyProxy>();
#endif

#if NLOG_43
        public static void AddTargetToConfig(LoggingConfiguration config, object targetProxy)
            => NLogCommon<LoggingConfiguration>.AddDatadogTargetNLog43To45(config, targetProxy);
#elif (!NLOG_45 && !NLOG_50)
        public static void AddTargetToConfig(LoggingConfiguration config, object targetProxy)
            => NLogCommon<LoggingConfiguration>.AddDatadogTargetNLogPre43(config, targetProxy);
#endif

        public class TestSink : IDirectSubmissionLogSink
        {
            public ConcurrentQueue<DirectSubmissionLogEvent> Events { get; } = new();

            public void EnqueueLog(DirectSubmissionLogEvent logEvent)
            {
                Events.Enqueue(logEvent);
            }

            public void Start()
            {
            }

            public Task FlushAsync()
            {
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                return Task.CompletedTask;
            }
        }
    }
}
#endif
