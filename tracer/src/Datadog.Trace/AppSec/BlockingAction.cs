// <copyright file="BlockingAction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Trace.AppSec;

internal record BlockingAction
{
    public const string BlockDefaultActionName = "block";

    public const string BlockRequestType = "block_request";

    public const string RedirectRequestType = "redirect_request";

    public const string GenerateStackType = "generate_stack";

    public bool IsPermanentRedirect => StatusCode == 301;

    public string ContentType { get; set; }

    public int StatusCode { get; set; }

    public string ResponseContent { get; set; }

    public string RedirectLocation { get; set; }

    public bool IsRedirect { get; set; }
}
