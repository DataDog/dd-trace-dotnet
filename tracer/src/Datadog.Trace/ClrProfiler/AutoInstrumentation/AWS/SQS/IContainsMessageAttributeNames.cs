// <copyright file="IContainsMessageAttributeNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// To be used for receive request ducktyping
    /// </summary>
    internal interface IContainsMessageAttributeNames
    {
        /// <summary>
        /// Gets or sets the list of message attributes that are going to be requested with the messages
        /// </summary>
        List<string> MessageAttributeNames { get; set;  }
    }
}
