// <copyright file="GraphQLSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

internal sealed class GraphQLSecurity
{
    private static GraphQLSecurity _instance;

    private Dictionary<IScope, Dictionary<string, List<Dictionary<string, object>>>> scopeResolvers;

    private GraphQLSecurity()
    {
        scopeResolvers = new();
    }

    private Dictionary<string, List<Dictionary<string, object>>> GetScopeResolvers(IScope scope)
    {
        if (!scopeResolvers.TryGetValue(scope, out var resolvers))
        {
            resolvers = new();
            scopeResolvers.Add(scope, resolvers);
        }

        return resolvers;
    }

    private void RemoveScopeResolvers(IScope scope)
    {
        scopeResolvers.Remove(scope);
    }

    public static GraphQLSecurity GetInstance()
    {
        if (_instance == null)
        {
            _instance = new GraphQLSecurity();
        }

        return _instance;
    }

    private static void RegisterResolverCall(IScope scope, string resolverName, Dictionary<string, object> arguments)
    {
        var resolvers = GraphQLSecurity.GetInstance().GetScopeResolvers(scope);

        if (!resolvers.TryGetValue(resolverName, out var resolverCalls))
        {
            resolverCalls = new List<Dictionary<string, object>>();
        }

        try
        {
            resolvers.Add(resolverName, resolverCalls);
        }
        catch (ArgumentException)
        {
        }

        // Add the current resolver call with arguments
        resolverCalls.Add(arguments);
    }

    public static void RunSecurity(Scope scope)
    {
        var security = Security.Instance;
        var asmEnabled = security.Settings.Enabled;
        if (asmEnabled)
        {
            var allResolvers = GraphQLSecurity.PopScope(scope);
            var args = new Dictionary<string, object> { { "graphql.server.all_resolvers", allResolvers } };
#if NETFRAMEWORK
            var securityCoordinator = new SecurityCoordinator(security, HttpContext.Current, scope.Span);
            securityCoordinator.CheckAndBlock(args);
#else
            var securityCoordinator = new SecurityCoordinator(security, CoreHttpContextStore.Instance.Get(), scope.Span);
            var result = securityCoordinator.RunWaf(args);
            securityCoordinator.CheckAndBlock(result);
#endif
        }
    }

    public static Dictionary<string, List<Dictionary<string, object>>> PopScope(IScope scope)
    {
        var resolvers = GraphQLSecurity.GetInstance().GetScopeResolvers(scope);
        GraphQLSecurity.GetInstance().RemoveScopeResolvers(scope);
        return resolvers;
    }

    public static void RegisterResolver<TContext, TNode>(TContext context, TNode node, bool v5V7 = false)
        where TContext : IExecutionContext
        where TNode : IExecutionNode
    {
        var scope = Tracer.Instance.ActiveScope;
        var resolverName = node.Name;

        // If no name is specified, it's not a resolver
        // If no arguments are provided, nothing to scan for the WAF
        if (string.IsNullOrEmpty(resolverName) || node.Field.Arguments is null)
        {
            return;
        }

        Dictionary<string, object> resolverArguments = new();
        foreach (var arg in node.Field.Arguments)
        {
            try
            {
                var name = string.Empty;
                object value = null;

                if (v5V7 && arg.TryDuckCast<GraphQLArgumentProxy>(out var argumentV5V7))
                {
                    name = argumentV5V7.Name.StringValue;
                    value = GetArgumentValue(context, argumentV5V7.Value);
                }
                else if (!v5V7 && arg.TryDuckCast<ArgumentProxy>(out var argument))
                {
                    name = argument.NameNode.Name;

                    // if Value is VariableReference
                    if (argument.Value.TryDuckCast<VariableReferenceProxy>(out var variableRef))
                    {
                        value = GetVariableValue(context, variableRef.Name);
                    }
                    else if (argument.Value.TryDuckCast<GraphQLValueProxy>(out var argValue))
                    {
                        value = argValue.Value;
                    }
                }
                else
                {
                    // An unknown type of Argument is in the AST
                    break;
                }

                resolverArguments.Add(name, value);
            }
            catch
            {
                // Failed to add the argument to the resolver args list
            }
        }

        RegisterResolverCall(scope, resolverName, resolverArguments);
    }

    private static object GetArgumentValue<TContext>(TContext context, object arg)
        where TContext : IExecutionContext
    {
        if (arg is null)
        {
            return null;
        }

        object value = null;

        if (arg.TryDuckCast<ASTNode>(out var node))
        {
            value = node.Kind switch
            {
                ASTNodeKindProxy.Variable => GetVariableValue(context, arg.DuckCast<GraphQLVariableProxy>().Name.StringValue),
                ASTNodeKindProxy.StringValue => arg.DuckCast<GraphQLValueProxy>().Value.ToString(),
                ASTNodeKindProxy.IntValue => int.Parse(arg.DuckCast<GraphQLValueProxy>().Value.ToString()),
                ASTNodeKindProxy.FloatValue => float.Parse(arg.DuckCast<GraphQLValueProxy>().Value.ToString()),
                ASTNodeKindProxy.BooleanValue => bool.Parse(arg.DuckCast<GraphQLValueProxy>().Value.ToString()),
                ASTNodeKindProxy.EnumValue => arg.DuckCast<GraphQLValueNameProxy>().Name.StringValue,
                ASTNodeKindProxy.ListValue => arg.DuckCast<GraphQLValueListProxy>().Values.Select(x => GetArgumentValue<TContext>(context, x)).ToList(),
                ASTNodeKindProxy.ObjectValue => arg.DuckCast<GraphQLValueObjectProxy>().Fields!.Select(x => GetArgumentValue<TContext>(context, x)).ToList(),

                _ => null
            };
        }

        return value;
    }

    private static object GetVariableValue<TContext>(TContext context, string name)
        where TContext : IExecutionContext
    {
        foreach (var v in context.Variables)
        {
            if (v.TryDuckCast<Variable>(out var var))
            {
                if (var.Name == name)
                {
                    return var.Value;
                }
            }
        }

        return null;
    }
}
