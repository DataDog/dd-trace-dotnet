// <copyright file="HotChocolateSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.HotChocolate.ASM;

internal abstract class HotChocolateSecurity
{
    public static void ScanQuery(IQueryRequest request)
    {
        if (Tracer.Instance.ActiveScope is null)
        {
            return;
        }

        var doc = request.Query;
        if (!doc.TryDuckCast<QueryDocument>(out var document))
        {
            return;
        }

        // Get root node of the document and do a depth search of resolvers
        foreach (var node in document.Document.Definitions)
        {
            DepthSearchOperationNode(node, request.VariableValues);
        }
    }

    private static void DepthSearchOperationNode(object obj, IDictionary<string, object> variables)
    {
        if (!obj.TryDuckCast<SyntaxNodeProxy>(out var node))
        {
            return;
        }

        switch (node.Kind)
        {
            case SyntaxKindProxy.OperationDefinition when obj.TryDuckCast<SelectionsNodeProxy>(out var opNode):
            {
                foreach (var selectionSetSelection in opNode.SelectionSet.Selections)
                {
                    DepthSearchOperationNode(selectionSetSelection, variables);
                }

                break;
            }

            case SyntaxKindProxy.Field when obj.TryDuckCast<FieldNodeProxy>(out var fieldNode):
            {
                if (fieldNode.Arguments.Any())
                {
                    var resolverName = GetFieldNodeName(fieldNode, obj);
                    var resolverArguments = new Dictionary<string, object>();

                    foreach (var argument in fieldNode.Arguments)
                    {
                        var name = GetName(argument);
                        var value = GetArgumentValue(argument, variables);
                        resolverArguments.Add(name, value);
                    }

                    GraphQLSecurityCommon.RegisterResolverCall(Tracer.Instance.ActiveScope, resolverName, resolverArguments);
                }

                DepthSearchOperationNode(fieldNode.SelectionSet, variables);
                break;
            }

            // All other nodes with an unknown type
            default:
            {
                if (obj.TryDuckCast<SelectionSetNode>(out var unknownSelectionSetNode))
                {
                    foreach (var n in unknownSelectionSetNode.Selections)
                    {
                        DepthSearchOperationNode(n, variables);
                    }
                }
                else if (obj.TryDuckCast<SelectionsNodeProxy>(out var unknownSelectionNode))
                {
                    DepthSearchOperationNode(unknownSelectionNode.SelectionSet, variables);
                }

                break;
            }
        }
    }

    private static string GetName(object node)
    {
        if (node.TryDuckCast<NamedNodeProxy>(out var namedNode))
        {
            return namedNode.Name.Value;
        }

        return string.Empty;
    }

    private static string GetFieldNodeName(FieldNodeProxy fieldNode, object node)
    {
        if (!string.IsNullOrEmpty(fieldNode.Alias.Value))
        {
            return fieldNode.Alias.Value;
        }

        return GetName(node);
    }

    private static object GetArgumentValue(object obj, IDictionary<string, object> variables)
    {
        if (obj is null || !obj.TryDuckCast<SyntaxNodeProxy>(out var node))
        {
            return null;
        }

        var value = node.Kind switch
        {
            SyntaxKindProxy.Argument => GetArgumentValue(obj.DuckCast<ValueNodeProxy>().Value, variables),
            SyntaxKindProxy.Variable => GetVariableValue(GetName(obj), variables),
            SyntaxKindProxy.StringValue or SyntaxKindProxy.EnumValue => obj.DuckCast<ValueNodeProxy>().Value.ToString(),
            SyntaxKindProxy.IntValue => int.Parse(obj.DuckCast<ValueNodeProxy>().Value.ToString() ?? string.Empty),
            SyntaxKindProxy.BooleanValue => bool.Parse(obj.DuckCast<ValueNodeProxy>().Value.ToString() ?? string.Empty),
            SyntaxKindProxy.FloatValue => float.Parse(obj.DuckCast<ValueNodeProxy>().Value.ToString() ?? string.Empty, CultureInfo.InvariantCulture.NumberFormat),
            SyntaxKindProxy.ListValue => (obj.DuckCast<ItemsNodeProxy>().Items ?? Array.Empty<object>()).Select(x => GetArgumentValue(x, variables)).ToList(),
            SyntaxKindProxy.ObjectValue => obj.DuckCast<ObjectValueNodeProxy>().Fields.ToDictionary(GetName, x => GetArgumentValue(x, variables)),
            SyntaxKindProxy.ObjectField => GetArgumentValue(obj.DuckCast<ValueNodeProxy>().Value, variables),

            _ => null
        };

        return value;
    }

    private static object GetVariableValue(string name, IDictionary<string, object> variables)
    {
        foreach (var variable in variables)
        {
            if (variable.Key == name)
            {
                return GetArgumentValue(variable.Value, variables);
            }
        }

        return null;
    }
}
