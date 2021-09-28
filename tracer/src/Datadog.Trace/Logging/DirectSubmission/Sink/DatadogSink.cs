// <copyright file="DatadogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal class DatadogSink : BatchingSink, IDatadogSink
    {
        // Maximum size for a single log is 1MB, we are currently only estimating the message size in bytes
        // so stay conservative here
        internal const int MaxMessageSizeBytes = 800 * 1024;

        // Maximum content size per payload is 5MB compressed
        // Stay conservative with max payload size of 3MB
        internal const int MaxSizeBytes = (3 * 1024 * 1024);

        internal const int InitialBuilderSizeBytes = 500 * 1024; // 0.5 MB

        // These are specific to the JSON/HTTP formatting, so probably shouldn't be constants
        // but this will do for now
        private const char Prefix = '[';
        private const char Suffix = ']';
        private const char Separator = ',';

        private readonly IDatadogLogger _logger = DatadogLogging.GetLoggerFor<DatadogSink>();
        private readonly ILogsApi _api;
        private readonly LogFormatter _formatter;
        private readonly Action<DatadogLogEvent> _oversizeLogCallback;
        private StringBuilder _sb = new(InitialBuilderSizeBytes);

        public DatadogSink(ILogsApi api, LogFormatter formatter, BatchingSinkOptions sinkOptions)
            : this(api, formatter, sinkOptions, oversizeLogCallback: null)
        {
        }

        public DatadogSink(ILogsApi api, LogFormatter formatter, BatchingSinkOptions sinkOptions, Action<DatadogLogEvent> oversizeLogCallback)
            : base(sinkOptions)
        {
            _api = api;
            _formatter = formatter;
            _oversizeLogCallback = oversizeLogCallback;
        }

        /// <summary>
        /// Emit a batch of log events to Datadog logs-backend.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        protected override async Task EmitBatch(Queue<DatadogLogEvent> events)
        {
            try
            {
                if (events.Count == 0)
                {
                    return;
                }

                _sb.Append(Prefix);
                var previousLength = _sb.Length;
                var logCount = 0;

                foreach (var log in events)
                {
                    // TODO: Check for oversize log during serialization?
                    // Currently a rogue giant message still pays the serialization cost but is then thrown away
                    log.Format(_sb, _formatter);
                    var logLength = _sb.Length - previousLength;
                    logCount++;

                    // We should be using Encoding.UTF8.GetByteCount(), but that requires allocating
                    // the string for every individual log. If we have to do that, maybe this should
                    // be restructured generally
                    if (logLength > MaxMessageSizeBytes)
                    {
                        // remove the last event
                        _logger.Error("Log dropped as too large to send to logs intake: {Log}", _sb.ToString(previousLength, logLength));
                        _sb.Remove(previousLength, logLength);
                        logCount--;
                        _oversizeLogCallback?.Invoke(log);
                        continue;
                    }

                    if (_sb.Length > MaxSizeBytes)
                    {
                        _sb.Append(Suffix);
                        await SendLogsChunk(_sb.ToString(), logCount).ConfigureAwait(false);
                        logCount = 0;
                        _sb.Clear();
                        _sb.Append(Prefix);
                    }
                    else
                    {
                        _sb.Append(Separator);
                    }

                    previousLength = _sb.Length;
                }

                if (logCount > 0)
                {
                    // remove the final separator and replace with suffix
                    _sb.Remove(_sb.Length - 1, length: 1);
                    _sb.Append(Suffix);
                    await SendLogsChunk(_sb.ToString(), logCount).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error occured sending logs to Datadog");
            }

            // We use a "shared" string builder, as this method is called by the BatchingSink
            // and is invoked serially. Depending on the number of logs, it can grow to ~MaxSizeBytes
            // A rogue giant message could cause it to grow significantly more than this, to be on the
            // safe size, reset the string builder when we get very large
            // We don't use MaxMessageSizeBytes as the upper limit here, otherwise we will repeatedly
            // shrink and grow the builder when we are commonly hitting the trace size limit
            if (_sb.Capacity > MaxMessageSizeBytes + InitialBuilderSizeBytes)
            {
                _sb = new StringBuilder(InitialBuilderSizeBytes);
            }
            else
            {
                _sb.Clear();
            }
        }

        private async Task SendLogsChunk(string formattedLogs, int logCount)
        {
            var content = Encoding.UTF8.GetBytes(formattedLogs);
            await _api.SendLogsAsync(new ArraySegment<byte>(content), logCount).ConfigureAwait(false);
        }

        protected override void AdditionalDispose()
        {
            _api.Dispose();
        }
    }
}
