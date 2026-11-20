// <copyright file="IPubsubMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    /// <summary>
    /// Duck-typed interface for Google.Cloud.PubSub.V1.PubsubMessage
    /// </summary>
    internal interface IPubsubMessage : IDuckType
    {
        /// <summary>
        /// Gets the message attributes. MapField{string, string} implements IDictionary{string, string}.
        /// </summary>
        IDictionary<string, string>? Attributes { get; }

        /// <summary>
        /// Gets the message data.
        /// </summary>
        IByteString? Data { get; }

        /// <summary>
        /// Gets the message id assigned by the server (empty on the publish side).
        /// </summary>
        string? MessageId { get; }

        /// <summary>
        /// Gets the server-assigned publish time (only meaningful on received messages).
        /// </summary>
        IPubsubTimestamp? PublishTime { get; }

        /// <summary>
        /// Calculates the serialized size of the message in bytes.
        /// </summary>
        int CalculateSize();
    }

    /// <summary>
    /// Duck-typed interface for Google.Protobuf.ByteString
    /// </summary>
    internal interface IByteString
    {
        /// <summary>
        /// Gets the length of the byte string in bytes.
        /// </summary>
        int Length { get; }
    }

    /// <summary>
    /// Duck-typed interface for Google.Protobuf.WellKnownTypes.Timestamp
    /// </summary>
    internal interface IPubsubTimestamp
    {
        /// <summary>
        /// Converts the timestamp to a DateTime in UTC.
        /// </summary>
        System.DateTime ToDateTime();
    }
}
