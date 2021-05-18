// <copyright file="HttpRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.HttpOverStreams
{
    internal class HttpRequest : HttpMessage
    {
        public HttpRequest(string verb, string host, string path, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            Verb = verb;
            Host = host;
            Path = path;
        }

        public string Verb { get; }

        public string Host { get; }

        public string Path { get; }
    }
}
