// <copyright file="IPublishRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    /// <summary>
    /// PublishRequest interface for ducktyping
    /// </summary>
    internal interface IPublishRequest : IAmazonSNSRequestWithTopicArn, IContainsMessageAttributes
    {
    }
}
