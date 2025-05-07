// <copyright file="IHttpWebRequestMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

/// <summary>
/// Duck type for HttpWebRequestMessage
/// https://github.com/aws/aws-sdk-net/blob/41a9184cb88ddf671cf35b276cc74d545aca49a7/sdk/src/Core/Amazon.Runtime/Pipeline/HttpHandler/_netstandard/HttpRequestMessageFactory.cs#L385
/// </summary>
internal interface IHttpWebRequestMessage
{
    Uri? RequestUri { get; }
}
