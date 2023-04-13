// <copyright file="GraphQLSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net.ASM;

internal abstract class GraphQLSecurity
{
    public static void RegisterResolver<TContext, TNode>(TContext context, TNode node, bool v5V7 = false)
        where TContext : IExecutionContext
        where TNode : IExecutionNode
    {
        var scope = Tracer.Instance.ActiveScope;
        if (scope is null)
        {
            return;
        }

        var resolverName = node.Name;

        // If no name is specified, it's not a resolver
        // If no arguments are provided, nothing to scan for the WAF
        if (string.IsNullOrEmpty(resolverName) || node.Field.Arguments is null || !node.Field.Arguments.Any())
        {
            return;
        }

        Dictionary<string, object> resolverArguments = new();
        foreach (var arg in node.Field.Arguments)
        {
            try
            {
                string name;
                object value = null;

                if (v5V7 && arg.TryDuckCast<GraphQLArgumentProxy>(out var argumentV5V7))
                {
                    name = argumentV5V7.Name.StringValue;
                    value = GetArgumentValue(context, argumentV5V7.Value);
                }
                else if (!v5V7)
                {
                    object toDuckValue;

                    // For the version 3 and 4 of GraphQL
                    if (arg.TryDuckCast<ArgumentProxy>(out var argumentV4))
                    {
                        name = argumentV4.NameNode.Name;
                        toDuckValue = argumentV4.Value;
                    }

                    // For the version 2 of GraphQL
                    else if (arg.TryDuckCast<ArgumentProxyV2>(out var argumentV2))
                    {
                        name = argumentV2.NamedNode.Name;
                        toDuckValue = argumentV2.Value;
                    }
                    else
                    {
                        // Failed to cast as argument
                        break;
                    }

                    // if Value is VariableReference
                    if (toDuckValue.TryDuckCast<VariableReferenceProxy>(out var variableRef))
                    {
                        value = GetVariableValue(context, variableRef.Name);
                    }
                    else if (toDuckValue.TryDuckCast<GraphQLValueProxy>(out var argValue))
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

        GraphQLSecurityCommon.RegisterResolverCall(scope, resolverName, resolverArguments);
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
                ASTNodeKindProxy.ListValue => arg.DuckCast<GraphQLValueListProxy>().Values.Select(x => GetArgumentValue(context, x)).ToList(),
                ASTNodeKindProxy.ObjectValue => arg.DuckCast<GraphQLValueObjectProxy>().Fields!.Select(x => GetArgumentValue(context, x)).ToList(),

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
