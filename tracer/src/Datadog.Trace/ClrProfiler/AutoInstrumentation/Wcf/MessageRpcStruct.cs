// <copyright file="MessageRpcStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf;

[DuckCopy]
internal struct MessageRpcStruct
{
    /// <summary>
    /// Gets the message object
    /// </summary>
    [DuckField(Name = "Request")]
    public IMessage Request;

    /// <summary>
    /// Gets the OperatonContext object
    /// </summary>
    [DuckField(Name = "OperationContext")]
    public IOperationContextStruct OperationContext;
}
