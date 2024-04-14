// <copyright file="BatchUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Sink
{
    internal class BatchUploader : IBatchUploader
    {
        private const int MaxSinglePayloadSize = 1 * 1024 * 1024;
        private const int MaxTotalPayloadSize = 5 * 1024 * 1024;
        private const int InitialBuilderSizeBytes = 10 * 1024;
        internal const int InitialPayloadSizeBytes = 100 * 1024;

        private const int InitialClosingBracketsLength = 2;
        private const int SeparatorLength = 1;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<BatchUploader>();

        private readonly IBatchUploadApi _api;
        private readonly StringBuilder _sb;

        private byte[] _serializedPayloads = new byte[InitialPayloadSizeBytes];

        private BatchUploader(IBatchUploadApi api)
        {
            _api = api;
            _sb = new StringBuilder(InitialBuilderSizeBytes);
        }

        public static BatchUploader Create(IBatchUploadApi api)
        {
            return new BatchUploader(api);
        }

        public async Task Upload(IEnumerable<string> payloads)
        {
            try
            {
                foreach (var batch in GetBatches(payloads))
                {
                    await _api.SendBatchAsync(batch).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to upload batch");
            }
            finally
            {
                _sb.Clear();
            }
        }

        private IEnumerable<ArraySegment<byte>> GetBatches(IEnumerable<string> payloads)
        {
            _sb.Clear();
            _sb.Append('[');

            var totalBatchSize = InitialClosingBracketsLength;
            foreach (var payload in payloads)
            {
                var payloadSize = Encoding.UTF8.GetByteCount(payload);
                if (payloadSize >= MaxSinglePayloadSize)
                {
                    Log.Warning("Big payload detected, skipping");
                    continue;
                }

                if (totalBatchSize + payloadSize > MaxTotalPayloadSize)
                {
                    yield return FinalizeBatch(totalBatchSize);

                    totalBatchSize = InitialClosingBracketsLength;
                    _sb.Clear();
                    _sb.Append('[');
                }

                _sb.Append(payload);
                _sb.Append(',');
                totalBatchSize += payloadSize + SeparatorLength;
            }

            var noPayloadsAdded = _sb.Length == 1;
            if (noPayloadsAdded)
            {
                yield break;
            }

            yield return FinalizeBatch(totalBatchSize);
        }

        private ArraySegment<byte> FinalizeBatch(int totalBatchSize)
        {
            OverwriteSeparator();

            if (_serializedPayloads.Length < totalBatchSize)
            {
                Array.Resize(ref _serializedPayloads, totalBatchSize);
            }

            var message = _sb.ToString();
            Encoding.UTF8.GetBytes(_sb.ToString(), 0, message.Length, _serializedPayloads, 0);
            var finalizeBatch = new ArraySegment<byte>(_serializedPayloads, 0, totalBatchSize);
            return finalizeBatch;

            void OverwriteSeparator()
            {
                _sb[_sb.Length - 1] = ']';
                totalBatchSize--;
            }
        }
    }
}
