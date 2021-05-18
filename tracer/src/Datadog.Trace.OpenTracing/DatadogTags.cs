// <copyright file="DatadogTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// This set of tags is used by the OpenTracing compatible tracer to set DataDog specific fields.
    /// </summary>
    public static class DatadogTags
    {
        /// <summary>
        /// This tag sets the service name
        /// </summary>
        public const string ServiceName = "service.name";

        /// <summary>
        /// This tag sets the service version
        /// </summary>
        public const string ServiceVersion = "service.version";

        /// <summary>
        /// This tag sets resource name
        /// </summary>
        public const string ResourceName = "resource.name";

        /// <summary>
        /// This tag sets the span type
        /// </summary>
        public const string SpanType = "span.type";
    }
}
