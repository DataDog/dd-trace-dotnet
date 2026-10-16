// <copyright file="IReceiveMessageResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    [DuckType("Amazon.SQS.Model.ReceiveMessageResponse", "AWSSDK.SQS")]
    internal interface IReceiveMessageResponse : IDuckType
    {
        IList? Messages { get; } // <IMessage>
    }

    [DuckType("Amazon.SQS.Model.Message", "AWSSDK.SQS")]
    internal interface IMessage : IContainsMessageAttributes
    {
        Dictionary<string, string?>? Attributes { get; set; }
    }
}
