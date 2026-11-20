// <copyright file="GetRcmRequestExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    internal static class GetRcmRequestExtensions
    {
        internal static bool Matches(this GetRcmRequest request, GetRcmResponse response)
        {
            var requestBackendClientState = request.Client.State.BackendClientState;
            var responseBackendClientstate = response.Targets.Signed.Custom.OpaqueBackendState;
            return requestBackendClientState == responseBackendClientstate;
        }

        /// <summary>
        /// Deserializes a request of either remote configuration client: the managed client sends the backend client
        /// state as a string, and the libdatadog client as an array of the bytes of the string.
        /// </summary>
        internal static GetRcmRequest DeserializeRcmRequest(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            var request = JObject.Load(reader);
            if (request["client"]?["state"]?["backend_client_state"] is JArray bytes)
            {
                bytes.Replace(Encoding.UTF8.GetString(bytes.Select(b => (byte)b).ToArray()));
            }

            return request.ToObject<GetRcmRequest>();
        }
    }
}
