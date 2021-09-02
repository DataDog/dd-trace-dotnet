// <copyright file="AspNetCoreMvcSpanExpectation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetCoreMvcSpanExpectation : WebServerSpanExpectation
    {
        public AspNetCoreMvcSpanExpectation(
            string serviceName,
            string serviceVersion,
            string operationName,
            string resourceName,
            string statusCode,
            string httpMethod)
            : base(
                serviceName,
                serviceVersion,
                operationName,
                resourceName,
                SpanTypes.Web,
                statusCode,
                httpMethod)
        {
        }

        public override bool Matches(MockTracerAgent.Span span)
        {
            var spanUri = GetTag(span, Tags.HttpUrl);
            if (spanUri == null || !spanUri.Contains(OriginalUri))
            {
                return false;
            }

            return base.Matches(span);
        }
    }
}
