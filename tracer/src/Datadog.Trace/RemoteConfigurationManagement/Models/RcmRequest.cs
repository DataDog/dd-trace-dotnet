// <copyright file="RcmRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Models;

internal class RcmRequest
{
    private RcmRequest()
    {
    }

    [JsonProperty("client")]
    public RcmClient Client { get; set; }

    [JsonProperty("cached_target_files")]
    public string[] CachedTargetFiles { get; set; }

    public static RcmRequest Create(
        string[] products,
        string serviceName,
        string serviceVersion,
        string environment,
        string runtimeId)
    {
        return new RcmRequest()
        {
            Client = new RcmClient()
            {
                Id = runtimeId,
                IsTracer = true,
                Name = serviceName,
                Products = products,
                State = new { },
                Version = TracerConstants.AssemblyVersion,
                ClientTracer = new RcmTracerClient()
                {
                    Language = TracerConstants.Language,
                    Env = environment,
                    RuntimeId = runtimeId,
                    Service = serviceName,
                    AppVersion = serviceVersion,
                    TracerVersion = TracerConstants.AssemblyVersion
                },
            },
            CachedTargetFiles = Array.Empty<string>()
        };
    }

    public ArraySegment<byte> AsArraySegment()
    {
        return new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)));
    }
}
