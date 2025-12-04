// <copyright file="OpenTelemetryHttpTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SemanticConventions;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class OpenTelemetryHttpTags : HttpTags
    {
        // Do not create a corresponding tag
        public override string InstrumentationName { get; set; }

        [Tag(OpenTelemetrySemanticConventions.HttpMethod)]
        public override string HttpMethod { get; set; }

        [Tag(OpenTelemetrySemanticConventions.HttpUrl)]
        public override string HttpUrl { get; set; }

        [Tag(OpenTelemetrySemanticConventions.HttpStatusCode)]
        public override string HttpStatusCode { get; set; }

        [Tag(OpenTelemetrySemanticConventions.OutHost)]
        public override string Host { get; set; }

        [Tag(OpenTelemetrySemanticConventions.OutPort)]
        public string Port { get; set; }
    }
}
