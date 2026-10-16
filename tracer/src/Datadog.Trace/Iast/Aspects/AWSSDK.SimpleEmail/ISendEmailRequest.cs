// <copyright file="ISendEmailRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

[DuckType("Amazon.SimpleEmail.Model.SendEmailRequest", "AWSSDK.SimpleEmail")]
internal interface ISendEmailRequest
{
    IMessage? Message { get; }
}

[DuckType("Amazon.SimpleEmail.Model.Message", "AWSSDK.SimpleEmail")]
internal interface IMessage
{
    IBody? Body { get; }
}

[DuckType("Amazon.SimpleEmail.Model.Body", "AWSSDK.SimpleEmail")]
internal interface IBody
{
    IHtml? Html { get; }
}

[DuckType("Amazon.SimpleEmail.Model.Content", "AWSSDK.SimpleEmail")]
internal interface IHtml
{
    string? Data { get; }
}
