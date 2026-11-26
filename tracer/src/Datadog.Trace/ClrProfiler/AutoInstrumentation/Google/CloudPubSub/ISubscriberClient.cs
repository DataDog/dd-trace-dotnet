// <copyright file="ISubscriberClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    /// <summary>
    /// Duck-typed interface for Google.Cloud.PubSub.V1.SubscriberClient
    /// </summary>
    internal interface ISubscriberClient
    {
        /// <summary>
        /// Gets the name of the subscription this subscriber reads from.
        /// </summary>
        ISubscriptionName? SubscriptionName { get; }
    }

    /// <summary>
    /// Duck-typed interface for Google.Cloud.PubSub.V1.SubscriptionName
    /// </summary>
    internal interface ISubscriptionName
    {
        /// <summary>
        /// Gets the subscription id (the final component of the subscription name).
        /// </summary>
        string? SubscriptionId { get; }
    }
}
