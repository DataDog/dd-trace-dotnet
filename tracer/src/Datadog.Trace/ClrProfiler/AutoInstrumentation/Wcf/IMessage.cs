// <copyright file="IMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Channels.Message interface for duck-typing
    /// </summary>
    internal interface IMessage
    {
        /// <summary>
        /// Gets the properties dictionary
        /// </summary>
        IDictionary<string, object?>? Properties { get; }

        /// <summary>
        /// Gets the message headers object
        /// </summary>
        IMessageHeaders? Headers { get; }
    }
}
