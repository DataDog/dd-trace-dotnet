// <copyright file="Visitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.DataFormat
{
    internal static class Visitor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Visitor));

        public static bool DepthFirstSearch(Node rootNode, Predicate<string> func)
        {
            var stack = new NodeHolder[10];
            var i = 0;
            stack[i] = new NodeHolder() { Loop = null, Node = rootNode };
            while (i >= 0)
            {
                switch (stack[i].Node.Type)
                {
                    case NodeType.Map:
                        stack[i].Loop ??= stack[i].Node.MapValue.Values.GetEnumerator();

                        if (stack[i].Loop.MoveNext())
                        {
                            var nextNode = stack[i].Loop.Current;
                            i++;
                            stack[i] = new NodeHolder() { Loop = null, Node = nextNode };
                        }
                        else
                        {
                            i--;
                        }

                        break;
                    case NodeType.List:
                        stack[i].Loop ??= stack[i].Node.ListValue.GetEnumerator();

                        if (stack[i].Loop.MoveNext())
                        {
                            var nextNode = stack[i].Loop.Current;
                            i++;
                            stack[i] = new NodeHolder() { Loop = null, Node = nextNode };
                        }
                        else
                        {
                            i--;
                        }

                        break;
                    case NodeType.String:
                        if (func(stack[i].Node.StringValue))
                        {
                            return true;
                        }

                        i--;
                        break;

                    default:
                        Log.Warning("Ignoring unknown value of type: {Type}", stack[i].Node.Type);
                        break;
                }
            }

            return false;
        }

        public static Node Map(Node rootNode, Func<Node, Node> func)
        {
            switch (rootNode.Type)
            {
                case NodeType.Map:
                    var newDict = rootNode.MapValue.ToDictionary(x => x.Key, x => Map(x.Value, func));
                    return Node.NewMap(newDict);

                case NodeType.List:
                    var newList = rootNode.ListValue.Select(x => Map(x, func)).ToList();

                    return Node.NewList(newList);
                case NodeType.String:
                    return func(rootNode);

                default:
                    Log.Error("Unknown value of type: {Type}", rootNode.Type);
                    throw new Exception($"Unknown value of type: {rootNode.Type}");
            }
        }

        private struct NodeHolder
        {
            public Node Node;
            public IEnumerator<Node> Loop;
        }
    }
}
