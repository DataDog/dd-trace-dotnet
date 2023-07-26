// <copyright file="UriAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
#if NET6_0_OR_GREATER
using Microsoft.Extensions.Options;
#endif

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System;

#pragma warning disable CS0618 // Type or member is obsolete
/// <summary> uri class aspects </summary>
[AspectClass("System,System.Runtime", AspectFilter.StringOptimization)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class UriAspect
{
    /// <summary>
    /// Uri .ctor(System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the URI.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String)", AspectFilter.StringLiterals)]
    public static Uri Init(string uriBase)
    {
        var result = new Uri(uriBase);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
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
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, uriText);
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
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, relativeUri);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The relative URI string.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.Uri)")]
    public static Uri Init(Uri uriBase, Uri relativeUri)
    {
        var result = new Uri(uriBase, relativeUri);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, relativeUri.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="dontEscape">dontEscape parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.Boolean)", AspectFilter.StringLiterals)]
    public static Uri Init(string uriBase, bool dontEscape)
    {
        var result = new Uri(uriBase, dontEscape);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        return result;
    }

    /// <summary>
    /// Uri .ctor(System.Uri,System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="uriKind">UriKind parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriKind)", AspectFilter.StringLiterals)]
    public static Uri Init(string uriBase, UriKind uriKind)
    {
        var result = new Uri(uriBase, uriKind);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        return result;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Uri .ctor(System.Uri,System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="options">UriCreationOptions parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriCreationOptions)", AspectFilter.StringLiterals)]
    public static Uri Init(string uriBase, in UriCreationOptions options)
    {
        var result = new Uri(uriBase, in options);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        return result;
    }
#endif

    /// <summary>
    /// Uri TryCreate aspect.
    /// </summary>
    /// <param name="uri">The base URI used to resolve the relative URI.</param>
    /// <param name="kind">The kind of uri.</param>
    /// <param name="uriCreated">The uri created.</param>
    /// <returns>True if the uri was created.</returns>
    [AspectMethodReplace("System.Uri::TryCreate(System.String,System.UriKind,System.Uri)", AspectFilter.StringLiterals)]
    public static bool TryCreate(string uri, UriKind kind, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(uri, kind, out uriCreated);
        if (uriCreated is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, uri);
        }

        return result;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Uri TryCreate aspect.
    /// </summary>
    /// <param name="uri">The base URI used to resolve the relative URI.</param>
    /// <param name="options">The options of uri.</param>
    /// <param name="uriCreated">The uri created.</param>
    /// <returns>True if the uri was created.</returns>
    [AspectMethodReplace("System.Uri::TryCreate(System.String,System.UriCreationOptions,System.Uri ByRef)", AspectFilter.StringLiterals)]
    public static bool TryCreate(string uri, in UriCreationOptions options, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(uri, options, out uriCreated);
        if (uriCreated is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, uri);
        }

        return result;
    }
#endif

    /// <summary>
    /// Uri TryCreate aspect.
    /// </summary>
    /// <param name="baseUri">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The options of uri.</param>
    /// <param name="uriCreated">The uri created.</param>
    /// <returns>True if the uri was created.</returns>
    [AspectMethodReplace("System.Uri::TryCreate(System.Uri,System.String,System.Uri)")]
    public static bool TryCreate(Uri? baseUri, string? relativeUri, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(baseUri, relativeUri, out uriCreated);
        if (uriCreated is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, baseUri?.OriginalString, relativeUri);
        }

        return result;
    }

    /// <summary>
    /// Uri TryCreate aspect.
    /// </summary>
    /// <param name="baseUri">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The options of uri.</param>
    /// <param name="uriCreated">The uri created.</param>
    /// <returns>True if the uri was created.</returns>
    [AspectMethodReplace("System.Uri::TryCreate(System.Uri,System.Uri,System.Uri)")]
    public static bool TryCreate(Uri? baseUri, Uri? relativeUri, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(baseUri, relativeUri, out uriCreated);
        if (uriCreated is not null)
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, baseUri?.OriginalString, relativeUri?.OriginalString);
        }

        return result;
    }

    /// <summary>
    /// Uri UnescapeDataString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::UnescapeDataString(System.String)", AspectFilter.StringLiterals)]
    public static string UnescapeDataString(string uri)
    {
        var result = Uri.UnescapeDataString(uri);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        return result;
    }

    /// <summary>
    /// Uri EscapeUriString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::EscapeUriString(System.String)", AspectFilter.StringLiterals)]
    public static string EscapeUriString(string uri)
    {
#pragma warning disable SYSLIB0013 // obsolete
        var result = Uri.EscapeUriString(uri);
#pragma warning restore 0168
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        return result;
    }

    /// <summary>
    /// Uri EscapeDataString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::EscapeDataString(System.String)", AspectFilter.StringLiterals)]
    public static string EscapeDataString(string uri)
    {
        var result = Uri.EscapeDataString(uri);
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        return result;
    }

    /// <summary>
    /// Uri GetAbsoluteUri aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The absolute URI string represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_AbsoluteUri()")]
    public static string GetAbsoluteUri(Uri instance)
    {
        var result = instance.AbsoluteUri;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri GetAbsolutePath aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The absolute path of the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_AbsolutePath()")]
    public static string GetAbsolutePath(Uri instance)
    {
        var result = instance.AbsolutePath;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri GetLocalPath aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The local operating-system representation of the URI path represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_LocalPath()")]
    public static string GetLocalPath(Uri instance)
    {
        var result = instance.LocalPath;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri MakeRelative aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <param name="uri">The uri argument.</param>
    /// <returns>The relative Uri.</returns>
    [AspectMethodReplace("System.Uri::MakeRelative(System.Uri)")]
    public static string? MakeRelative(Uri instance, Uri uri)
    {
        var result = instance.MakeRelative(uri);
        if (!string.IsNullOrWhiteSpace(result))
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri.OriginalString);
        }

        return result;
    }

    /// <summary>
    /// Uri MakeRelativeUri aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <param name="uri">The uri argument.</param>
    /// <returns>The relative Uri.</returns>
    [AspectMethodReplace("System.Uri::MakeRelativeUri(System.Uri)")]
    public static Uri? MakeRelativeUri(Uri instance, Uri uri)
    {
        var result = instance.MakeRelativeUri(uri);
        if (!string.IsNullOrWhiteSpace(result?.OriginalString))
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result!.OriginalString, uri.OriginalString);
        }

        return result;
    }

    /// <summary>
    /// Uri GetHost aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The DNS host name or IP address specified in the URI represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_Host()")]
    public static string GetHost(Uri instance)
    {
        var result = instance.Host;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri GetPathAndQuery aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI path and query represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_PathAndQuery()")]
    public static string GetPathAndQuery(Uri instance)
    {
        var result = instance.PathAndQuery;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri GetAuthority aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI authority represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_Authority()")]
    public static string GetAuthority(Uri instance)
    {
        var result = instance.Authority;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }

    /// <summary>
    /// Uri GetQuery aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>The URI query represented by the System.Uri instance.</returns>
    [AspectMethodReplace("System.Uri::get_Query()")]
    public static string GetQuery(Uri instance)
    {
        var result = instance.Query;
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
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
        PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        return result;
    }
}
