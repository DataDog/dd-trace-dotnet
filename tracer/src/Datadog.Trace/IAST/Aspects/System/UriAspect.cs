// <copyright file="UriAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System;

#pragma warning disable CS0618 // Type or member is obsolete
/// <summary> uri class aspects </summary>
[AspectClass("mscorlib,netstandard,System.Private.CoreLib,System.Runtime", AspectFilter.StringOptimization)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class UriAspect
{
    /// <summary>
    /// Uri .ctor(System.String) aspect.
    /// </summary>
    /// <param name="uriText">A string that identifies the resource to be represented by the System.Uri instance.</param>
    /// <returns>The initialized System.Uri instance created using the specified URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String)")]
    public static Uri Init(string uriText)
    {
        var result = new Uri(uriText);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.String,System.Boolean) aspect.
    /// </summary>
    /// <param name="uriText">A string that identifies the resource to be represented by the System.Uri instance.</param>
    /// <param name="escape">true to escape the URI string; otherwise, false.</param>
    /// <returns>The initialized System.Uri instance created using the specified URI string and escape value.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.Boolean)")]
    public static Uri Init(string uriText, bool escape)
    {
        var result = new Uri(uriText, escape);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.String,System.Boolean) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="uriText">A string that identifies the resource to be represented by the System.Uri instance.</param>
    /// <param name="escape">true to escape the URI string; otherwise, false.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI, relative URI string, and escape value.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.String,System.Boolean)")]
    public static Uri Init(Uri uriBase, string uriText, bool escape)
    {
        var result = new Uri(uriBase, uriText, escape);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriBase, uriText);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.String,System.UriKind) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="urikind">One of the System.UriKind values that specifies the type of the URI.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and URI kind.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriKind)")]
    public static Uri Init(string uriBase, UriKind urikind)
    {
        var result = new Uri(uriBase, urikind);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriBase);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The relative URI string.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.String)")]
    public static Uri Init(Uri uriBase, string relativeUri)
    {
        var result = new Uri(uriBase, relativeUri);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriBase, relativeUri);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.Uri) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The relative URI.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.Uri)")]
    public static Uri Init(Uri uriBase, Uri relativeUri)
    {
        var result = new Uri(uriBase, relativeUri);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriBase, relativeUri);
        return result;
    }

    /// <summary>
    /// Uri GetAbsoluteUri aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The absolute URI string represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetAbsoluteUri()")]
    public static string GetAbsoluteUri(Uri instance)
    {
        var result = instance.AbsoluteUri;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetAbsolutePath aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The absolute path of the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetAbsolutePath()")]
    public static string GetAbsolutePath(Uri instance)
    {
        var result = instance.AbsolutePath;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetLocalPath aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The local operating-system representation of the URI path represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetLocalPath()")]
    public static string GetLocalPath(Uri instance)
    {
        var result = instance.LocalPath;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetHost aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The DNS host name or IP address specified in the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetHost()")]
    public static string GetHost(Uri instance)
    {
        var result = instance.Host;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetPathAndQuery aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI path and query represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetPathAndQuery()")]
    public static string GetPathAndQuery(Uri instance)
    {
        var result = instance.PathAndQuery;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetAuthority aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI authority represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetAuthority()")]
    public static string GetAuthority(Uri instance)
    {
        var result = instance.Authority;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetQuery aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI query represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetQuery()")]
    public static string GetQuery(Uri instance)
    {
        var result = instance.Query;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetScheme aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The scheme name of the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetScheme()")]
    public static string GetScheme(Uri instance)
    {
        var result = instance.Scheme;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri GetOriginalString aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The display string representation of the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::GetOriginalString()")]
    public static string GetOriginalString(Uri instance)
    {
        var result = instance.OriginalString;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }

    /// <summary>
    /// Uri ToString aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>A string that represents the current System.Uri instance.</returns>
    [AspectMethodReplace("System.Object::ToString()", "System.Uri")]
    public static string ToString(Uri instance)
    {
        string result = instance.ToString();
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
        return result;
    }
}
