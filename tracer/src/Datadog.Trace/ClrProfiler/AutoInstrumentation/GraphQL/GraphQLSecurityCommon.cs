// <copyright file="GraphQLSecurityCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL;

internal sealed class GraphQLSecurityCommon
{
    private static readonly Lazy<GraphQLSecurityCommon> LazyInstance = new(() => new GraphQLSecurityCommon());

    private readonly ConcurrentDictionary<IScope, Dictionary<string, object>> _scopeResolvers;

    private GraphQLSecurityCommon()
    {
        _scopeResolvers = new ConcurrentDictionary<IScope, Dictionary<string, object>>();
    }

    private static GraphQLSecurityCommon Instance
        => LazyInstance.Value;

    private static Dictionary<string, object> PopScope(IScope scope)
    {
        var resolvers = Instance.GetScopeResolvers(scope);
        Instance.RemoveScopeResolvers(scope);
        return resolvers;
    }

    private Dictionary<string, object> GetScopeResolvers(IScope scope)
    {
        if (!_scopeResolvers.TryGetValue(scope, out var resolvers))
        {
            resolvers = new Dictionary<string, object>();
            _scopeResolvers.TryAdd(scope, resolvers);
        }

        return resolvers;
    }

    private void RemoveScopeResolvers(IScope scope)
    {
        _scopeResolvers.TryRemove(scope, out _);
    }

    /// <summary>
    /// Register a called resolver with its arguments for a given scope into the <see cref="_scopeResolvers"/> dictionary.
    /// </summary>
    internal static void RegisterResolverCall(IScope scope, string resolverName, Dictionary<string, object> arguments)
    {
        var resolvers = Instance.GetScopeResolvers(scope);

        if (!resolvers.TryGetValue(resolverName, out var resolverCalls))
        {
            resolverCalls = new List<object> { arguments };
            resolvers.Add(resolverName, resolverCalls);
        }
        else
        {
            // Add the current resolver call with arguments
            ((List<object>)resolverCalls).Add(arguments);
        }
    }

    /// <summary>
    /// Run the WAF for the given scope of the GraphQL request.
    /// </summary>
    public static void RunSecurity(Scope scope)
    {
        if (!IsEnabled())
        {
            return;
        }

        var security = Security.Instance;
        var allResolvers = PopScope(scope);

        var args = new Dictionary<string, object> { { "graphql.server.all_resolvers", allResolvers } };
#if NETFRAMEWORK
        var httpContext = HttpContext.Current;
#else
        var httpContext = CoreHttpContextStore.Instance.Get();
#endif
        var securityCoordinator = new SecurityCoordinator(security, httpContext, scope.Span);
        securityCoordinator.Check(args);
    }

    /// <summary>
    /// Check if we need to perform an analysis for the GraphQL request.
    /// </summary>
    /// <returns>True if ASM is enabled and the current request is not a WebSocket connection.</returns>
    public static bool IsEnabled()
    {
        // Check if ASM is Enabled
        if (!Security.Instance.Enabled)
        {
            return false;
        }

        // Check if this is a WebSocket connection
        // WebSocket connexion are not supported yet
#if NETFRAMEWORK
        var httpContext = HttpContext.Current;
        var isWebsocket = httpContext is not null && httpContext.IsWebSocketRequest;
#else
        var httpContext = CoreHttpContextStore.Instance.Get();
        var isWebsocket = httpContext is not null && httpContext.WebSockets.IsWebSocketRequest;
#endif
        return !isWebsocket;
    }
}
