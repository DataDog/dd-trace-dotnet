// <copyright file="Log4NetHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using log4net.Core;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Log4Net
{
    internal class Log4NetHelper
    {
#if LOG4NET_2
        public static DirectSubmissionLog4NetAppender GetAppender(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel level)
            => new(sink, level);

        public static ILoggingEventDuck DuckCastLogEvent(LoggingEvent logEvent)
            => logEvent.DuckCast<ILoggingEventDuck>();
#else
        public static DirectSubmissionLog4NetLegacyAppender GetAppender(IDirectSubmissionLogSink sink, DirectSubmissionLogLevel level)
            => new(sink, level);

        public static ILoggingEventLegacyDuck DuckCastLogEvent(LoggingEvent logEvent)
            => logEvent.DuckCast<ILoggingEventLegacyDuck>();
#endif

        internal class TestSink : IDirectSubmissionLogSink
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
