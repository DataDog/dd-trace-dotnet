// <copyright file="UriAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System;

/// <summary> uri class aspects </summary>
[AspectClass("System,System.Runtime", [AspectFilter.StringOptimization])]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class UriAspect
{
    /// <summary>
    /// Uri .ctor(System.String) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the URI.</param>
    /// <returns>The initialized System.Uri instance created.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String)", AspectFilter.StringLiteral_1)]
    public static Uri Init(string uriBase)
    {
        var result = new Uri(uriBase);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

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
#pragma warning disable CS0618 // Type or member is obsolete
        var result = new Uri(uriBase, uriText, escape);
#pragma warning restore CS0618 // Type or member is obsolete
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, uriText);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, relativeUri);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary>
    /// Uri .ctor(Uri uriBase, Uri relativeUri) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="relativeUri">The relative URI string.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.Uri)")]
    public static Uri Init(Uri uriBase, Uri relativeUri)
    {
        var result = new Uri(uriBase, relativeUri);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase.OriginalString, relativeUri.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary>
    /// Uri .ctor(System.String,System.Boolean) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="dontEscape">dontEscape parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.Boolean)", AspectFilter.StringLiteral_1)]
    public static Uri Init(string uriBase, bool dontEscape)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var result = new Uri(uriBase, dontEscape);
#pragma warning restore CS0618 // Type or member is obsolete
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

        return result;
    }

    /// <summary>
    /// Uri .ctor(System.String,System.UriKin) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="uriKind">UriKind parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriKind)", AspectFilter.StringLiteral_1)]
    public static Uri Init(string uriBase, UriKind uriKind)
    {
        var result = new Uri(uriBase, uriKind);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

        return result;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Uri .ctor(System.String,System.UriCreationOptions) aspect.
    /// </summary>
    /// <param name="uriBase">The base URI used to resolve the relative URI.</param>
    /// <param name="options">UriCreationOptions parameter.</param>
    /// <returns>The initialized System.Uri instance created using the specified base URI and relative URI string.</returns>
    [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriCreationOptions)", AspectFilter.StringLiteral_1)]
    public static Uri Init(string uriBase, in UriCreationOptions options)
    {
        var result = new Uri(uriBase, in options);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result.OriginalString, uriBase);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(Init)}");
        }

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
    [AspectMethodReplace("System.Uri::TryCreate(System.String,System.UriKind,System.Uri)", AspectFilter.StringLiteral_0)]
    public static bool TryCreate(string uri, UriKind kind, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(uri, kind, out uriCreated);
        try
        {
            if (uriCreated is not null)
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, uri);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(TryCreate)}");
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
    [AspectMethodReplace("System.Uri::TryCreate(System.String,System.UriCreationOptions,System.Uri)", AspectFilter.StringLiteral_0)]
    public static bool TryCreate(string uri, in UriCreationOptions options, out Uri? uriCreated)
    {
        var result = Uri.TryCreate(uri, options, out uriCreated);
        try
        {
            if (uriCreated is not null)
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, uri);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(TryCreate)}");
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
        try
        {
            if (uriCreated is not null)
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, baseUri?.OriginalString, relativeUri);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(TryCreate)}");
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
        try
        {
            if (uriCreated is not null)
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(uriCreated.OriginalString, baseUri?.OriginalString, relativeUri?.OriginalString);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(TryCreate)}");
        }

        return result;
    }

    /// <summary>
    /// Uri UnescapeDataString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::UnescapeDataString(System.String)", AspectFilter.StringLiteral_0)]
    public static string UnescapeDataString(string uri)
    {
        var result = Uri.UnescapeDataString(uri);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(UnescapeDataString)}");
        }

        return result;
    }

    /// <summary>
    /// Uri EscapeUriString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::EscapeUriString(System.String)", AspectFilter.StringLiteral_0)]
    public static string EscapeUriString(string uri)
    {
#pragma warning disable SYSLIB0013 // obsolete
        var result = Uri.EscapeUriString(uri);
#pragma warning restore SYSLIB0013
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(EscapeUriString)}");
        }

        return result;
    }

    /// <summary>
    /// Uri EscapeDataString aspect.
    /// </summary>
    /// <param name="uri">The uri as string.</param>
    /// <returns>The resulting method result.</returns>
    [AspectMethodReplace("System.Uri::EscapeDataString(System.String)", AspectFilter.StringLiteral_0)]
    public static string EscapeDataString(string uri)
    {
        var result = Uri.EscapeDataString(uri);
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(EscapeDataString)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetAbsoluteUri)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetAbsolutePath)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetLocalPath)}");
        }

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
#pragma warning disable CS0618 // Type or member is obsolete
        var result = instance.MakeRelative(uri);
#pragma warning restore CS0618 // Type or member is obsolete
        try
        {
            if (!string.IsNullOrWhiteSpace(result))
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri.OriginalString);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(MakeRelative)}");
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
        try
        {
            if (!string.IsNullOrWhiteSpace(result?.OriginalString))
            {
                PropagationModuleImpl.PropagateResultWhenInputTainted(result!.OriginalString, uri.OriginalString);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(MakeRelativeUri)}");
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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetHost)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetPathAndQuery)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetAuthority)}");
        }

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
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(GetQuery)}");
        }

        return result;
    }

    /// <summary>
    /// Uri ToString aspect.
    /// </summary>
    /// <param name="instance">The System.Uri instance.</param>
    /// <returns>A string that represents the current System.Uri instance.</returns>
    [AspectMethodReplace("System.Object::ToString()", "System.Uri")]
    public static string? ToString(object? instance)
    {
        // We want the null reference exception to be launched here if target is null
        var result = instance!.ToString();
        try
        {
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, (instance as Uri)?.OriginalString);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(UriAspect)}.{nameof(ToString)}");
        }

        return result;
    }
}
