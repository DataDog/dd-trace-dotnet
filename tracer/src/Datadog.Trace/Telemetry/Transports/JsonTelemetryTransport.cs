﻿// <copyright file="JsonTelemetryTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Telemetry.Transports
{
    internal abstract class JsonTelemetryTransport : ITelemetryTransport
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonTelemetryTransport>();
        internal static readonly JsonSerializerSettings SerializerSettings = new() { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy(), } };

        private readonly IApiRequestFactory _requestFactory;
        private readonly Uri _endpoint;

        protected JsonTelemetryTransport(IApiRequestFactory requestFactory)
        {
            _requestFactory = requestFactory;
            _endpoint = _requestFactory.GetEndpoint(TelemetryConstants.TelemetryPath);
        }

        protected string GetEndpointInfo() => _requestFactory.Info(_endpoint);

        public async Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            try
            {
                // have to buffer in memory so we know the content length
                var serializedData = SerializeTelemetry(data);
                var bytes = Encoding.UTF8.GetBytes(serializedData);

                var request = _requestFactory.Create(_endpoint);

                request.AddHeader(TelemetryConstants.ApiVersionHeader, data.ApiVersion);
                request.AddHeader(TelemetryConstants.RequestTypeHeader, data.RequestType);

                using var response = await request.PostAsync(new ArraySegment<byte>(bytes), "application/json").ConfigureAwait(false);
                if (response.StatusCode is >= 200 and < 300)
                {
                    Log.Debug("Telemetry sent successfully");
                    return TelemetryPushResult.Success;
                }

                if (response.StatusCode == 404)
                {
                    Log.Debug("Error sending telemetry: 404. Disabling further telemetry, as endpoint '{Endpoint}' not found", GetEndpointInfo());
                    return TelemetryPushResult.FatalError;
                }

                Log.Debug<string, int>("Error sending telemetry to '{Endpoint}' {StatusCode} ", GetEndpointInfo(), response.StatusCode);
                return TelemetryPushResult.TransientFailure;
            }
            catch (Exception ex) when (IsFatalException(ex))
            {
                Log.Information(ex, "Error sending telemetry data, unable to communicate with '{Endpoint}'", GetEndpointInfo());
                return TelemetryPushResult.FatalError;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Error sending telemetry data to '{Endpoint}'", GetEndpointInfo());
                return TelemetryPushResult.TransientFailure;
            }
        }

        public abstract string GetTransportInfo();

        // Internal for testing
        internal static string SerializeTelemetry(TelemetryData data)
        {
            return JsonConvert.SerializeObject(data, Formatting.None, SerializerSettings);
        }

        private static bool IsFatalException(Exception ex)
        {
            return ex.IsSocketException()
                || ex is WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } };
        }
    }
}
