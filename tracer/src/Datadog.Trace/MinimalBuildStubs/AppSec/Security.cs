// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace Datadog.Trace.AppSec;

internal class Security
{
    public static Security Instance { get; } = new();

    public SecuritySettings Settings { get; } = new();

    public bool AppsecEnabled => false;

    public bool IsTrackUserEventsEnabled => false;

    public bool IsAnonUserTrackingMode => false;

    public string WafRuleFileVersion => string.Empty;

    public string DdlibWafVersion => string.Empty;

    public string? InitializationError => string.Empty;

    public void CheckPathParams(HttpContext httpContext, Span rootSpan, RouteValueDictionary routeValues)
    {
    }

    public void CheckPathParamsFromAction(HttpContext httpContext, Span span, IList<ParameterDescriptor>? actionDescriptorParameters, RouteValueDictionary routeDataValues)
    {
    }
}
