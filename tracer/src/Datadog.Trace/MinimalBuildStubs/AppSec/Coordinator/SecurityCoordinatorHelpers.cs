// <copyright file="SecurityCoordinatorHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Coordinator;

internal static class SecurityCoordinatorHelpers
{
    public static object CheckBody(this Security security, HttpContext contextHttpContext, Span currentSpan, object resultValue, bool response) => null;

    public static void CheckReturnedHeaders(Security security, Span span, IHeaderDictionary responseHeaders)
    {
    }
}
