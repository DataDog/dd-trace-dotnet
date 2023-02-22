// <copyright file="CIWriterFileSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// This class is for debugging purposes only.
    /// </summary>
    internal sealed class CIWriterFileSender : ICIVisibilityProtocolWriterSender
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIWriterFileSender>();

        public CIWriterFileSender()
        {
            Log.Information("CIWriterFileSender Initialized.");
        }

        public Task SendPayloadAsync(EventPlatformPayload payload)
        {
            switch (payload)
            {
                case CIVisibilityProtocolPayload ciVisibilityProtocolPayload:
                    return SendPayloadAsync(ciVisibilityProtocolPayload);
                case MultipartPayload multipartPayload:
                    return SendPayloadAsync(multipartPayload);
                default:
                    Util.ThrowHelper.ThrowNotSupportedException("Payload is not supported.");
                    return Task.FromException(new NotSupportedException("Payload is not supported."));
            }
        }

        private Task SendPayloadAsync(CIVisibilityProtocolPayload payload)
        {
            var str = Path.Combine(Path.GetTempPath(), $"civiz-{Guid.NewGuid():n}");

            var msgPackBytes = payload.ToArray();
            var msgPackFile = str + ".mpack";
            File.WriteAllBytes(msgPackFile, msgPackBytes);
            Log.Debug("File written: {File}", msgPackFile);

            var json = Vendors.MessagePack.MessagePackSerializer.ToJson(msgPackBytes);
            var jsonFile = str + ".json";
            File.WriteAllText(jsonFile, json);
            Log.Debug("File written: {File}", jsonFile);

            return Task.CompletedTask;
        }

        private Task SendPayloadAsync(MultipartPayload payload)
        {
            var str = Path.Combine(Path.GetTempPath(), $"multipart-{Guid.NewGuid():n}");
            foreach (var item in payload.ToArray())
            {
                byte[] bytes = null;

                if (item.ContentInStream is { } stream)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                else if (item.ContentInBytes is { } arraySegment)
                {
                    bytes = arraySegment.ToArray();
                }

                if (bytes is not null)
                {
                    var msgPackFile = str + $"{item.Name}.mpack";
                    File.WriteAllBytes(msgPackFile, bytes);
                    Log.Debug("File written: {File}", msgPackFile);

                    var json = Vendors.MessagePack.MessagePackSerializer.ToJson(bytes);
                    var jsonFile = str + $"{item.Name}.json";
                    File.WriteAllText(jsonFile, json);
                    Log.Debug("File written: {File}", jsonFile);
                }
            }

            return Task.CompletedTask;
        }
    }
}
