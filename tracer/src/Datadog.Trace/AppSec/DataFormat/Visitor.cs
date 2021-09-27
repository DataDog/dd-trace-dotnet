// <copyright file="Visitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.DataFormat
{
    internal static class Visitor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Visitor));

        public static bool DepthFirstSearch(Node rootNode, Predicate<string> func)
        {
            switch (rootNode.Type)
            {
                case NodeType.Map:
                    if (rootNode.MapValue.Values.Any(valueItem => DepthFirstSearch(valueItem, func)))
                    {
                        return true;
                    }

                    break;
                case NodeType.List:
                    if (rootNode.ListValue.Any(valueItem => DepthFirstSearch(valueItem, func)))
                    {
                        return true;
                    }

                    break;
                case NodeType.String:
                    return func(rootNode.StringValue);

                default:
                    Log.Warning("Ignoring unknown value of type: {Type}", rootNode.Type);
                    break;
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
    }
}
