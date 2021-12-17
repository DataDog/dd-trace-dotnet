// <copyright file="IMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf
{
    /// <summary>
    /// System.ServiceModel.Channels.Message interface for duck-typing
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IMessage
    {
        /// <summary>
        /// Gets the properties dictionary
        /// </summary>
        public IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Gets the message headers object
        /// </summary>
        public IMessageHeaders Headers { get; }
    }
}
