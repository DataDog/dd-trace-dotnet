// <copyright file="GraphQLSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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

    public static Dictionary<string, List<Dictionary<string, object>>> PopScope(IScope scope)
    {
        var resolvers = GraphQLSecurity.GetInstance().GetScopeResolvers(scope);
        GraphQLSecurity.GetInstance().RemoveScopeResolvers(scope);
        return resolvers;
    }

    public static void RegisterResolver<TContext, TNode>(TContext context, TNode node)
        where TContext : IExecutionContext
        where TNode : IExecutionNode
    {
        Console.WriteLine("REGISTER A RESOLVER!!!!!!");
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
            if (arg.TryDuckCast<GraphQLArgumentProxy>(out var argument))
            {
                var name = argument.Name.StringValue;
                var value = GetArgumentValue(context, argument.Value);
                resolverArguments.Add(name, value);
            }
            else
            {
                // An unknown type of Argument is in the AST
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
                ASTNodeKindProxy.StringValue => arg.DuckCast<GraphQLValueProxy>().ToString(),
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
