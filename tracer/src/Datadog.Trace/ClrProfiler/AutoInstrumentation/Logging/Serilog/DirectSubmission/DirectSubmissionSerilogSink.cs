// <copyright file="DirectSubmissionSerilogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog
{
    /// <summary>
    /// Serilog Sink
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DirectSubmissionSerilogSink
    {
        private readonly IDatadogSink _sink;
        private readonly int _minimumLevel;

        internal DirectSubmissionSerilogSink(IDatadogSink sink, DirectSubmissionLogLevel minimumLevel)
        {
            _sink = sink;
            _minimumLevel = (int)minimumLevel;
        }

        /// <summary>
        /// Emit the provided log event to the sink
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Serilog.Events.LogEvent, Serilog" })]
        public void Emit(ILogEvent? logEvent)
        {
            if (logEvent is null)
            {
                return;
            }

            if ((int)logEvent.Level < _minimumLevel)
            {
                return;
            }

            _sink.EnqueueLog(new SerilogDatadogLogEvent(logEvent));
        }
    }
}
