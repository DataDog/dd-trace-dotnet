// <copyright file="AwsLambdaSdkTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging;

internal sealed partial class AwsLambdaSdkTags : AwsSdkTags
{
    [Tag(Trace.Tags.SpanKind)]
    public override string SpanKind => SpanKinds.Client;

    [Tag("functionname")]
    public string? FunctionName { get; set; }

    [Tag("aws.lambda")]
    public string? LambdaFunctionName => FunctionName;
}
