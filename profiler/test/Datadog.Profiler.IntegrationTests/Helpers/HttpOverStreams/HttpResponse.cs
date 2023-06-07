// <copyright file="HttpResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace Datadog.Profiler.IntegrationTests.Helpers.HttpOverStreams
{
    internal class HttpResponse : HttpMessage
    {
        public HttpResponse(int statusCode, string responseMessage, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            StatusCode = statusCode;
            ResponseMessage = responseMessage;
        }

        public int StatusCode { get; }

        public string ResponseMessage { get; }
    }
}
