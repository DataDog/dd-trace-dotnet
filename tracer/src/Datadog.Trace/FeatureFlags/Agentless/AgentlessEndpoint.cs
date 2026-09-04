// <copyright file="AgentlessEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// The agentless endpoint, derived from the Datadog site or a custom base URL. A class rather
/// than a struct so that "no endpoint" is <c>null</c> instead of a default instance whose
/// non-nullable <see cref="Uri"/> is null; it is built once per process, so the allocation is free.
/// </summary>
internal sealed class AgentlessEndpoint
{
    /// <summary>
    /// Canonical rules-based server path, appended to the managed CDN host and to custom base
    /// URLs that only supply an origin.
    /// </summary>
    internal const string DefaultPath = "/api/v2/feature-flagging/config/rules-based/server";

    /// <summary>
    /// The prefix prepended to the site to form the managed CDN host.
    /// </summary>
    internal const string ManagedHostPrefix = "ufc-server.ff-cdn.";

    /// <summary>
    /// The query parameter carrying the environment to request configuration for. Added to the
    /// managed endpoint only.
    /// </summary>
    internal const string EnvParameterName = "dd_env";

    private AgentlessEndpoint(Uri uri, bool isManaged)
    {
        Uri = uri;
        IsManaged = isManaged;
    }

    /// <summary>
    /// Gets the endpoint URI, without the environment. Use <see cref="BuildRequestUri"/> to get the
    /// URI to request.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// Gets a value indicating whether this is the endpoint derived from the site. The API key is
    /// only sent there: a custom endpoint reports its own authentication failure rather than
    /// having the credential leaked to it.
    /// </summary>
    public bool IsManaged { get; }

    /// <summary>
    /// Builds the endpoint. Without a custom <paramref name="baseUrl"/> the managed Datadog CDN
    /// endpoint is derived from the (lowercased) site, so staging and government sites resolve
    /// with no allowlist, and is the only endpoint <c>dd_env</c> is added to. A custom base URL that
    /// is an origin receives the canonical path; one that carries a path is used verbatim.
    /// <para>
    /// The environment is not part of the endpoint: it can be changed in code while the application
    /// runs, so it is applied per request by <see cref="BuildRequestUri"/> instead.
    /// </para>
    /// </summary>
    /// <param name="site">The Datadog site, for example <c>datadoghq.com</c>.</param>
    /// <param name="baseUrl">The configured endpoint override, or <c>null</c>.</param>
    /// <param name="endpoint">The resulting endpoint, or <c>null</c> when none could be built.</param>
    /// <param name="error">Why the configured base URL was rejected. Never contains the URL, which may carry credentials.</param>
    /// <returns><c>true</c> when an endpoint could be built.</returns>
    public static bool TryCreate(string? site, string? baseUrl, [NotNullWhen(true)] out AgentlessEndpoint? endpoint, out string? error)
    {
        endpoint = null;
        error = null;

        var configured = baseUrl?.Trim();
        if (StringUtil.IsNullOrEmpty(configured))
        {
            var trimmedSite = site?.Trim();
            if (StringUtil.IsNullOrEmpty(trimmedSite))
            {
                error = "No Datadog site is configured";
                return false;
            }

            // The site is concatenated into a host, so every character that can change what a URL means
            // has to be rejected before that happens. "@" is the dangerous one: it would make the rest of
            // the value the real host, and the API key would be sent there. "/", "?" and "#" would start a
            // path, query or fragment, and ":" a port or a scheme. Uri.TryCreate accepts several of these,
            // so it cannot be relied on to catch them. The other tracers reject the same set.
            foreach (var character in trimmedSite)
            {
                if (char.IsWhiteSpace(character) || character is '/' or '?' or '#' or '@' or ':')
                {
                    error = "The configured Datadog site is not valid";
                    return false;
                }
            }

            var managedHost = ManagedHostPrefix + StringUtil.ToLowerInvariant(trimmedSite);

            if (!Uri.TryCreate($"https://{managedHost}{DefaultPath}", UriKind.Absolute, out var managedUri))
            {
                error = "The configured Datadog site is not valid";
                return false;
            }

            endpoint = new AgentlessEndpoint(managedUri, isManaged: true);
            return true;
        }

        // Uri parsing rejects whitespace inside a host, but accepts it in a path or query, so a
        // URL carrying it there is malformed and has to be caught explicitly.
        foreach (var character in configured)
        {
            if (char.IsWhiteSpace(character))
            {
                error = "The configured Feature Flags agentless URL is not a valid URL";
                return false;
            }
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var custom) || StringUtil.IsNullOrEmpty(custom.Host))
        {
            error = "The configured Feature Flags agentless URL is not a valid absolute URL";
            return false;
        }

        // http is accepted for a custom endpoint only: pointing at one is an operator decision.
        if (custom.Scheme != Uri.UriSchemeHttps && custom.Scheme != Uri.UriSchemeHttp)
        {
            error = "The configured Feature Flags agentless URL must use HTTP or HTTPS";
            return false;
        }

        if (custom.AbsolutePath is "" or "/")
        {
            custom = new UriBuilder(custom) { Path = DefaultPath }.Uri;
        }

        endpoint = new AgentlessEndpoint(custom, isManaged: false);
        return true;
    }

    /// <summary>
    /// Returns the URI to request configuration for <paramref name="env"/>. The environment is
    /// added as a query parameter rather than baked into the endpoint, because it can change while
    /// the application runs.
    /// <para>
    /// A configured base URL is opaque: it is requested exactly as the operator wrote it, since it
    /// may hold credentials, routing, or a scope of its own. Only the managed endpoint carries
    /// <c>dd_env</c>, which matches the other tracers.
    /// </para>
    /// </summary>
    /// <param name="env">The current environment, or <c>null</c> when none is configured.</param>
    /// <returns>The URI to request.</returns>
    public Uri BuildRequestUri(string? env)
    {
        if (!IsManaged)
        {
            return Uri;
        }

        // Normalized the same way the tracer normalizes it before tagging spans, so that flag
        // targeting and span tags agree on what the environment is. It also bounds the value at 200
        // characters, which keeps a misconfigured environment from producing an unusable URL.
        // A value that normalizes to nothing is treated as no environment at all.
        var normalized = TraceUtil.NormalizeTag(env);
        if (StringUtil.IsNullOrEmpty(normalized))
        {
            return Uri;
        }

        var parameter = EnvParameterName + "=" + Uri.EscapeDataString(normalized);
        var builder = new UriBuilder(Uri);

        // The getter returns the query with its leading "?", while the setter prepends one of its
        // own on .NET Framework, so the existing query is trimmed before it is extended. A URL
        // ending in a bare "?" reports a query of "?", which the length check treats as no query.
        var existing = builder.Query;
        builder.Query = existing.Length > 1 ? existing.TrimStart('?') + "&" + parameter : parameter;
        return builder.Uri;
    }
}
