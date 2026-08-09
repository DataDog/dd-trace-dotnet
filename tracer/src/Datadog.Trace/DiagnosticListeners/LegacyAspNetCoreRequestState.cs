// <copyright file="LegacyAspNetCoreRequestState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Holds the state shared by the legacy ASP.NET Core request diagnostic events.
/// </summary>
internal sealed class LegacyAspNetCoreRequestState
{
    public LegacyAspNetCoreRequestState(Scope rootScope, LegacyAspNetCoreDiagnosticObserver.HttpRequestStruct request)
    {
        RootScope = rootScope;

        // The combined PathBase + Path, mirroring the .NET Core RequestTrackingFeature's OriginalPath.
        // We use the unescaped Value (like the .NET Core version) rather than ToUriComponent, so that this
        // never allocates for escaping; and PathBase is (almost) always empty at HttpRequestIn.Start,
        // so the concatenation itself rarely happens either.
        OriginalPath = Combine(request.PathBase.Value ?? string.Empty, request.Path.Value ?? string.Empty);
    }

    /// <summary>
    /// Gets the exact request scope created by the start event.
    /// </summary>
    public Scope RootScope { get; }

    /// <summary>
    /// Gets the original combined PathBase and Path, as captured at pipeline start.
    /// </summary>
    public string OriginalPath { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the first pipeline execution. Pipeline
    /// re-execution (e.g. exception-handler or status-code-pages middleware) fires additional
    /// MVC events that must not overwrite the root span's resource name or route tags
    /// </summary>
    public bool IsFirstPipelineExecution { get; set; } = true;

    /// <summary>
    /// Returns whether the request's current combined path still matches the one captured at pipeline
    /// start. A mismatch indicates the pipeline was re-executed against a different URL (e.g. a 404).
    /// </summary>
    /// <remarks>
    /// Mirrors <c>RequestTrackingFeature.MatchesOriginalPath</c>: we compare against the <em>combined</em>
    /// PathBase + Path (not each part separately) because middleware such as <c>Map</c>/<c>UsePathBase</c>
    /// can migrate a segment between PathBase and Path within a single, non-re-executed request. We split
    /// the stored <see cref="OriginalPath"/> at the current PathBase boundary using <see cref="string.Compare(string, int, string, int, int, StringComparison)"/>
    /// (the net461 equivalent of the .NET Core version's <c>StartsWithSegments</c> + span slice) so the
    /// comparison never allocates.
    /// </remarks>
    public bool MatchesOriginalPath(LegacyAspNetCoreDiagnosticObserver.HttpRequestStruct request)
    {
        var pathBase = request.PathBase.Value ?? string.Empty;
        var path = request.Path.Value ?? string.Empty;

        if (pathBase.Length == 0)
        {
            return string.Equals(OriginalPath, path, StringComparison.OrdinalIgnoreCase);
        }

        // Skip the first character of Path where it would be collapsed against a trailing '/' on
        // PathBase, matching Combine (and PathString.Add) so the two sides stay consistent.
        var pathStart = path.Length > 0 && pathBase[pathBase.Length - 1] == '/' ? 1 : 0;
        var pathLength = path.Length - pathStart;

        // OriginalPath == Combine(pathBase, path), checked without allocating the concatenation here.
        return OriginalPath.Length == pathBase.Length + pathLength
            && string.Compare(OriginalPath, 0, pathBase, 0, pathBase.Length, StringComparison.OrdinalIgnoreCase) == 0
            && string.Compare(OriginalPath, pathBase.Length, path, pathStart, pathLength, StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>
    /// Combines PathBase and Path the same way <c>PathString.Add</c> does on ASP.NET Core 2.x (the only
    /// versions that run on .NET Framework): when PathBase ends with a '/', the first character of Path
    /// is dropped at the join. We don't duck type Add() directly to avoid the awkward extra duck typing
    /// (it takes and returns the concrete PathString) for perf reasons.
    /// </summary>
    private static string Combine(string pathBase, string path)
    {
        if (pathBase.Length == 0)
        {
            return path;
        }

        if (path.Length == 0)
        {
            return pathBase;
        }

        return pathBase[pathBase.Length - 1] == '/'
                   ? pathBase + path.Substring(1)
                   : pathBase + path;
    }
}

#endif
