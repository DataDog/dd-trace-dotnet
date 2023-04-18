// <copyright file="GraphQLSecurityCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL;

internal sealed class GraphQLSecurityCommon
{
    private static GraphQLSecurityCommon _instance;

    private readonly Dictionary<IScope, Dictionary<string, object>> _scopeResolvers;

    private GraphQLSecurityCommon()
    {
        _scopeResolvers = new();
    }

    private static GraphQLSecurityCommon GetInstance()
    {
        return _instance ??= new GraphQLSecurityCommon();
    }

    private Dictionary<string, object> GetScopeResolvers(IScope scope)
    {
        if (!_scopeResolvers.TryGetValue(scope, out var resolvers))
        {
            resolvers = new();
            _scopeResolvers.Add(scope, resolvers);
        }

        return resolvers;
    }

    private void RemoveScopeResolvers(IScope scope)
    {
        _scopeResolvers.Remove(scope);
    }

    internal static void RegisterResolverCall(IScope scope, string resolverName, Dictionary<string, object> arguments)
    {
        var resolvers = GetInstance().GetScopeResolvers(scope);

        if (!resolvers.TryGetValue(resolverName, out var resolverCalls))
        {
            resolverCalls = new List<object>();
        }

        try
        {
            resolvers.Add(resolverName, resolverCalls);
        }
        catch (ArgumentException)
        {
        }

        // Add the current resolver call with arguments
        ((List<object>)resolverCalls).Add(arguments);
    }

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
        var securityCoordinator = new SecurityCoordinator(security, httpContext, scope.Span);
        securityCoordinator.CheckAndBlock(args);
#else
        var httpContext = CoreHttpContextStore.Instance.Get();
        var securityCoordinator = new SecurityCoordinator(security, httpContext, scope.Span);
        var result = securityCoordinator.RunWaf(args);
        securityCoordinator.CheckAndBlock(result);
#endif
    }

    private static Dictionary<string, object> PopScope(IScope scope)
    {
        var resolvers = GetInstance().GetScopeResolvers(scope);
        GetInstance().RemoveScopeResolvers(scope);
        return resolvers;
    }

    public static bool IsEnabled()
    {
#if NETFRAMEWORK
        var httpContext = HttpContext.Current;
        var isWebsocket = httpContext is not null && httpContext.IsWebSocketRequest;
#else
        var httpContext = CoreHttpContextStore.Instance.Get();
        var isWebsocket = httpContext is not null && httpContext.WebSockets.IsWebSocketRequest;
#endif
        return Security.Instance.Settings.Enabled && !isWebsocket;
    }
}
