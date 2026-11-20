// <copyright file="IPublisherClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    /// <summary>
    /// Duck-typed interface for Google.Cloud.PubSub.V1.PublisherClient
    /// </summary>
    internal interface IPublisherClient
    {
        /// <summary>
        /// Gets the name of the topic this publisher publishes to.
        /// </summary>
        ITopicName? TopicName { get; }
    }

    /// <summary>
    /// Duck-typed interface for Google.Cloud.PubSub.V1.TopicName
    /// </summary>
    internal interface ITopicName
    {
        /// <summary>
        /// Gets the topic id (the final component of the topic name).
        /// </summary>
        string? TopicId { get; }
    }
}
