// <copyright file="AgentlessEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// The agentless endpoint, derived from the Datadog site or a custom base URL.
/// </summary>
internal readonly struct AgentlessEndpoint
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

    private AgentlessEndpoint(Uri uri, bool isManaged)
    {
        Uri = uri;
        IsManaged = isManaged;
    }

    /// <summary>
    /// Gets the endpoint URI.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// Gets a value indicating whether this is the endpoint derived from the site. The API key is
    /// only sent there: a custom endpoint reports its own authentication failure rather than
    /// having the credential guessed onto it.
    /// </summary>
    public bool IsManaged { get; }

    /// <summary>
    /// Builds the endpoint. Without a custom <paramref name="baseUrl"/> the managed Datadog CDN
    /// endpoint is derived from the (lowercased) site, so staging and government sites resolve
    /// with no allowlist, and <c>dd_env</c> is added only when an environment is configured.
    /// A custom base URL that is an origin receives the canonical path; one that carries a path
    /// is used verbatim.
    /// </summary>
    /// <param name="site">The Datadog site, for example <c>datadoghq.com</c>.</param>
    /// <param name="env">The configured environment, or <c>null</c>.</param>
    /// <param name="baseUrl">The configured endpoint override, or <c>null</c>.</param>
    /// <param name="endpoint">The resulting endpoint.</param>
    /// <param name="error">Why the configured base URL was rejected. Never contains the URL, which may carry credentials.</param>
    /// <returns><c>true</c> when an endpoint could be built.</returns>
    public static bool TryCreate(string? site, string? env, string? baseUrl, out AgentlessEndpoint endpoint, out string? error)
    {
        endpoint = default;
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

            var managedHost = ManagedHostPrefix + trimmedSite!.ToLowerInvariant();
            if (managedHost.Contains("://") || HasWhitespace(managedHost))
            {
                error = "The configured Datadog site is not valid";
                return false;
            }

            if (!Uri.TryCreate($"https://{managedHost}{DefaultPath}", UriKind.Absolute, out var managedUri))
            {
                error = "The configured Datadog site is not valid";
                return false;
            }

            if (!StringUtil.IsNullOrEmpty(env))
            {
                managedUri = new UriBuilder(managedUri) { Query = "dd_env=" + Uri.EscapeDataString(env!) }.Uri;
            }

            endpoint = new AgentlessEndpoint(managedUri, isManaged: true);
            return true;
        }

        // A URL with internal whitespace is malformed, and Uri parsing is lenient enough to accept it.
        foreach (var character in configured!)
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

    private static bool HasWhitespace(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }
}
