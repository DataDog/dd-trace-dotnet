// <copyright file="SymbolUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal class SymbolUploader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolUploader));
        private readonly IBatchUploadApi _api;
        private readonly int _sizeLimit;
        private byte[] _payload;
        private int _byteIndex;

        private SymbolUploader(IBatchUploadApi api, int sizeLimit)
        {
            _api = api;
            _sizeLimit = sizeLimit;
            _payload = new byte[sizeLimit * 2];
        }

        public static SymbolUploader Create(IBatchUploadApi api, int sizeLimit)
        {
            return new SymbolUploader(api, sizeLimit);
        }

        public async Task<bool> SendSymbol(SymbolModel symbolModel)
        {
            try
            {
                var symbolAsString = JsonConvert.SerializeObject(symbolModel);
                var count = Encoding.UTF8.GetByteCount(symbolAsString);
                if (_byteIndex + count >= _sizeLimit)
                {
                    var newPayload = new byte[_byteIndex + count];
                    Array.Copy(_payload, 0, newPayload, 0, _byteIndex);
                    _payload = newPayload;
                }

                _byteIndex += Encoding.UTF8.GetBytes(symbolAsString, 0, symbolAsString.Length, _payload, _byteIndex);
                if (_byteIndex < _sizeLimit)
                {
                    return false;
                }

                await _api.SendBatchAsync(new ArraySegment<byte>(_payload)).ConfigureAwait(false);
                Array.Clear(_payload, 0, _byteIndex - 1);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to upload assembly symbol info {Assembly}", symbolModel.Scopes.FirstOrDefault().Name ?? "UNKNOWN");
                return false;
            }
        }
    }
}
