// <copyright file="IReceiveMessageResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// ReceiveMessageResponse interface for ducktyping
    /// </summary>
    internal interface IReceiveMessageResponse
    {
        internal interface IMessage
        {
            Dictionary<string, string> Attributes { get; set; }
        }

        List<IMessage> Messages { get; set; }
    }
}
