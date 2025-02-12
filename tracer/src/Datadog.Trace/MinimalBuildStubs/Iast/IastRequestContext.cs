// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    public void AddIastVulnerabilitiesToSpan(Span span)
    {
    }

    internal static void AddIastDisabledFlagToSpan(Span span)
    {
    }

    public void AddRequestData(HttpRequest httpContextRequest, RouteValueDictionary routeValues)
    {
    }

    public void AddRequestBody(object resultModel, object bodyExtracted)
    {
    }
}
