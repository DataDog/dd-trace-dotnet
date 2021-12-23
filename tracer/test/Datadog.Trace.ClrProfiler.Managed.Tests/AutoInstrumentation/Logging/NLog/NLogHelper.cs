// <copyright file="NLogHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETCOREAPP
using System.Collections.Concurrent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using NLog;
using NLog.Config;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.NLog
{
    internal static class NLogHelper
    {
#if NLOG_45
        public static DirectSubmissionNLogTarget CreateTarget(IDatadogSink sink, DirectSubmissionLogLevel minimumLevel)
            => new(sink, minimumLevel, SettingsHelper.GetFormatter());

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
#elif !NLOG_45
        public static void AddTargetToConfig(LoggingConfiguration config, object targetProxy)
            => NLogCommon<LoggingConfiguration>.AddDatadogTargetNLogPre43(config, targetProxy);
#endif

        public class TestSink : IDatadogSink
        {
            public ConcurrentQueue<DatadogLogEvent> Events { get; } = new();

            public void Dispose()
            {
            }

            public void EnqueueLog(DatadogLogEvent logEvent)
            {
                Events.Enqueue(logEvent);
            }
        }
    }
}
#endif
