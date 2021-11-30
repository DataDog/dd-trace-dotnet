// <copyright file="JsonTelemetryTransportBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Telemetry
{
    internal abstract class JsonTelemetryTransportBase : ITelemetryTransport
    {
        internal static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), }
        };

        public abstract Task<bool> PushTelemetry(TelemetryData data);

        protected string SerializeTelemetry(TelemetryData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.None, SerializerSettings);
        }
    }
}
