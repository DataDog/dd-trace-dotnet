// <copyright file="DatadogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal class DatadogSink : BatchingSink, IDatadogSink
    {
        // Maximum size for a single log is 1MB, we slightly on the cautious side
        internal const int MaxMessageSizeBytes = 1000 * 1024;

        // Maximum content size per payload is 5MB compressed
        // Stay conservative with max payload size of 3MB
        internal const int MaxTotalSizeBytes = (3 * 1024 * 1024);

        // Initial size of the per-event string builder
        // Should be big enough to handle _most_ logs to avoid too many initial resizes
        internal const int InitialBuilderSizeBytes = 10 * 1024; // 10 KB

        // Initial size of the full serialized set of logs
        // Should be big enough to handle _most_ cases to avoid too many resizes
        internal const int InitialAllLogsSizeBytes = 100 * 1024; // 0.1 MB

        // These are specific to the JSON/HTTP formatting, so probably shouldn't be constants
        // but this will do for now
        private const byte PrefixAsUtf8Byte = 0x5b; // '['
        private const byte SuffixAsUtf8Byte = 0x5D; // ']'
        private const byte SeparatorAsUtf8Byte = 0x2C; // ','

        private readonly IDatadogLogger _logger = DatadogLogging.GetLoggerFor<DatadogSink>();
        private readonly ILogsApi _api;
        private readonly LogFormatter _formatter;
        private readonly Action<DatadogLogEvent> _oversizeLogCallback;
        private readonly StringBuilder _logStringBuilder = new(InitialBuilderSizeBytes);
        private byte[] _serializedLogs = new byte[InitialAllLogsSizeBytes];
        private int _byteCount = 0;
        private int _logCount = 0;

        public DatadogSink(ILogsApi api, LogFormatter formatter, BatchingSinkOptions sinkOptions)
            : this(api, formatter, sinkOptions, oversizeLogCallback: null, sinkDisabledCallback: null)
        {
        }

        public DatadogSink(
            ILogsApi api,
            LogFormatter formatter,
            BatchingSinkOptions sinkOptions,
            Action<DatadogLogEvent> oversizeLogCallback,
            Action sinkDisabledCallback)
            : base(sinkOptions, sinkDisabledCallback)
        {
            _api = api;
            _formatter = formatter;
            _oversizeLogCallback = oversizeLogCallback;
        }

        /// <summary>
        /// Emit a batch of log events to Datadog logs-backend.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        protected override async Task<bool> EmitBatch(Queue<DatadogLogEvent> events)
        {
            var allSucceeded = true;
            try
            {
                if (events.Count == 0)
                {
                    return true;
                }

                // Add to the first log
                _serializedLogs[0] = PrefixAsUtf8Byte;
                _logCount = 0;
                _byteCount = 1;

                foreach (var log in events)
                {
                    // reset the string builder
                    _logStringBuilder.Clear();
                    if (_logStringBuilder.Capacity > MaxMessageSizeBytes)
                    {
                        // A rogue giant message could cause the builder to grow significantly more than MaxMessageSizeBytes
                        // so reset the string builder when we get very large
                        _logStringBuilder.Capacity = InitialBuilderSizeBytes;
                    }

                    log.Format(_logStringBuilder, _formatter);

                    // would be nice to avoid this, but can't see a way without direct UTF8 serialization (System.Text.Json)
                    // Currently a rogue giant message still pays the serialization cost but is then thrown away
                    var serializedLog = _logStringBuilder.ToString();

                    var logSize = Encoding.UTF8.GetByteCount(serializedLog);
                    if (logSize > MaxMessageSizeBytes)
                    {
                        // Note that the logs intake will actually accept oversized logs, and then truncate them to 1MB
                        // We could continue to send the log as long as the total size isn't over 5MB, but not sure if
                        // it's worth it or not, so excluding for now.
                        _logger.Error<int, string>("Log dropped as too large ({Size} bytes) to send to logs intake: {Log}", logSize, serializedLog);
                        _oversizeLogCallback?.Invoke(log);
                        continue;
                    }

                    var requiredTotalSize = _byteCount + logSize + 1; // + 1 for the separate/suffix
                    if (requiredTotalSize > _serializedLogs.Length && requiredTotalSize <= MaxTotalSizeBytes)
                    {
                        // grow the array
                        var newSize = Math.Min(_serializedLogs.Length * 2, MaxTotalSizeBytes);
                        var newArray = new byte[newSize];
                        Array.Copy(_serializedLogs, 0, newArray, 0, _serializedLogs.Length);
                        _serializedLogs = newArray;
                    }

                    if (requiredTotalSize > MaxTotalSizeBytes)
                    {
                        // send what we have, add the log to the subsequent chunk
                        var result = await ReplaceFinalSeparatorAndSendChunk().ConfigureAwait(false);
                        allSucceeded &= result;
                    }

                    // add the log to the batch
                    var bytesWritten = Encoding.UTF8.GetBytes(serializedLog, 0, serializedLog.Length, _serializedLogs, _byteCount);
                    Debug.Assert(bytesWritten == logSize, "Actual bytes written should equal the log size");

                    _byteCount += bytesWritten;

                    // add the separator
                    _serializedLogs[_byteCount] = SeparatorAsUtf8Byte;
                    _byteCount++;
                    _logCount++;
                }

                if (_logCount > 0)
                {
                    var result = await ReplaceFinalSeparatorAndSendChunk().ConfigureAwait(false);
                    allSucceeded &= result;
                }

                return allSucceeded;
            }
            catch (Exception e)
            {
                _logger.Error(e, "An error occured sending logs to Datadog");
                return false;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _api.Dispose();
        }

        private async Task<bool> ReplaceFinalSeparatorAndSendChunk()
        {
            // Too large for this batch, so replace final separator and send what we have
            Debug.Assert(_byteCount > 0, "Shouldn't ever be in a situation where we have 0 bytes");

            _serializedLogs[_byteCount - 1] = SuffixAsUtf8Byte;
            var result = await _api.SendLogsAsync(new ArraySegment<byte>(_serializedLogs, 0, _byteCount), _logCount).ConfigureAwait(false);

            // reset everything and on to the next log
            _logCount = 0;
            // keep the initial suffix
            _byteCount = 1;
            return result;
        }
    }
}
