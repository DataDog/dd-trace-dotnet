// <copyright file="CIWriterFileSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
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

        public Task<bool> Ping()
        {
            return Task.FromResult(true);
        }

        public Task SendPayloadAsync(EventsPayload payload)
        {
            var str = $"c:\\temp\\file-{Guid.NewGuid().ToString("n")}";

            var msgPackBytes = payload.ToArray();
            File.WriteAllBytes(str + ".mpack", msgPackBytes);

            var json = Vendors.MessagePack.MessagePackSerializer.ToJson(msgPackBytes);
            File.WriteAllText(str + ".json", json);

            return Task.CompletedTask;
        }
    }
}
