// <copyright file="ApiWebRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequestFactory : IApiRequestFactory
    {
        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new ApiWebRequest(WebRequest.CreateHttp(endpoint));
        }
    }
}
