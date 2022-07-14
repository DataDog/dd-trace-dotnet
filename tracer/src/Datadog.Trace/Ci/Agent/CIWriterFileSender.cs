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
    internal sealed class CIWriterFileSender : ICIAgentlessWriterSender
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CIWriterFileSender>();

        public CIWriterFileSender()
        {
            Log.Information("CIWriterFileSender Initialized.");
        }

        public Task SendPayloadAsync(CIVisibilityProtocolPayload payload)
        {
            var str = $"c:\\temp\\file-{Guid.NewGuid().ToString("n")}";

            var msgPackBytes = payload.ToArray();
            File.WriteAllBytes(str + ".mpack", msgPackBytes);

            var json = Vendors.MessagePack.MessagePackSerializer.ToJson(msgPackBytes);
            File.WriteAllText(str + ".json", json);

            return Task.CompletedTask;
        }

        public Task SendPayloadAsync(MultipartPayload payload)
        {
            var str = $"c:\\temp\\multipart-{Guid.NewGuid().ToString("n")}";
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
                    File.WriteAllBytes(str + $"{item.Name}.mpack", bytes);

                    var json = Vendors.MessagePack.MessagePackSerializer.ToJson(bytes);
                    File.WriteAllText(str + $"{item.Name}.json", json);
                }
            }

            return Task.CompletedTask;
        }
    }
}
