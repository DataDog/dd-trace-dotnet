// <copyright file="AwsDynamoDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsDynamoDbTags : AwsSdkTags
    {
        [Tag(Trace.Tags.TableName)]
        public string? TableName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;
    }
}
