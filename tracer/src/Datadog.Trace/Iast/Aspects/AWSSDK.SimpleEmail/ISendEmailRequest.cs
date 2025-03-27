// <copyright file="ISendEmailRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

internal interface ISendEmailRequest
{
    IMessage? Message { get; }
}

internal interface IMessage
{
    IBody? Body { get; }
}

internal interface IBody
{
    IHtml? Html { get; }
}

internal interface IHtml
{
    string? Data { get; }
}
