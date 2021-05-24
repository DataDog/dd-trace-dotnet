// <copyright file="SetResponseHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers.HttpMessageHandlers
{
    public class SetResponseHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;

        public SetResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public int RequestsCount { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestsCount++;
            return Task.FromResult(_response);
        }
    }
}
