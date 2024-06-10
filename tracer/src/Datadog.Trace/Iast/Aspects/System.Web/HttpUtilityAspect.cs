// <copyright file="HttpUtilityAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> WebClient class aspects </summary>
[AspectClass("System.Web;System.Runtime.Extensions;System.Web.HttpUtility", AspectType.Sink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpUtilityAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Web.HttpUtility::HtmlEncode(System.String)")]
    public static string? Review(string? parameter)
    {
        return IastModule.OnXssEscape(parameter);
    }

    /// <summary>
    /// Launches an Unvalidated Redirect vulnerability if the url is tainted
    /// </summary>
    /// <param name="path">the sensitive parameter of the method</param>
    /// <returns>the path</returns>
    [AspectMethodInsertBefore("System.Web.HttpUtility::Transfer(System.String)", 0)]
    [AspectMethodInsertBefore("System.Web.HttpUtility::Transfer(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.Web.HttpUtility::Execute(System.String)", 0)]
    [AspectMethodInsertBefore("System.Web.HttpUtility::Execute(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.Web.HttpUtility::Execute(System.String,System.TextWriter)", 1)]
    [AspectMethodInsertBefore("System.Web.HttpUtility::Execute(System.String,System.TextWriter,System.Boolean)", 2)]
    public static string? PathBasedAction(string? path)
    {
        return IastModule.OnUnvalidatedRedirectPath(path);
    }
}
