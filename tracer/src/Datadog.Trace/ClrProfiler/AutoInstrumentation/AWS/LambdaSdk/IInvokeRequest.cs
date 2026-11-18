// <copyright file="IInvokeRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;

/// <summary>Duck type for Amazon.Lambda.Model.InvokeRequest.</summary>
internal interface IInvokeRequest : IDuckType
{
    string? FunctionName { get; }

    IInvocationType? InvocationType { get; }

    string? ClientContextBase64 { get; set; }
}
