// <copyright file="Node.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.AppSec.DataFormat
{
    internal class Node
    {
        private Node(NodeType type, IReadOnlyDictionary<string, Node> mapValue, IReadOnlyList<Node> listValue, string stringValue)
        {
            Type = type;
            MapValue = mapValue;
            ListValue = listValue;
            StringValue = stringValue;
        }

        public NodeType Type { get; }

        public IReadOnlyDictionary<string, Node> MapValue { get; }

        public IReadOnlyList<Node> ListValue { get; }

        public string StringValue { get; }

        public static Node NewMap(IReadOnlyDictionary<string, Node> mapValue)
        {
            return new Node(NodeType.Map, mapValue, null, null);
        }

        public static Node NewList(IReadOnlyList<Node> listValue)
        {
            return new Node(NodeType.List, null, listValue, null);
        }

        public static Node NewString(string stringValue)
        {
            return new Node(NodeType.String, null, null, stringValue);
        }
    }
}
