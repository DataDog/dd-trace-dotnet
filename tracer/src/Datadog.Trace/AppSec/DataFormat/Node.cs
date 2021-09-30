// <copyright file="Node.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.AppSec.DataFormat
{
    internal class Node
    {
        private Node(NodeType type, Dictionary<string, Node> mapValue, List<Node> listValue, string stringValue)
        {
            Type = type;
            MapValue = mapValue;
            ListValue = listValue;
            StringValue = stringValue;
        }

        public NodeType Type { get; }

        public Dictionary<string, Node> MapValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public List<Node> ListValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public string StringValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public static Node NewMap(Dictionary<string, Node> mapValue)
        {
            return new Node(NodeType.Map, mapValue, null, null);
        }

        public static Node NewList(List<Node> listValue)
        {
            return new Node(NodeType.List, null, listValue, null);
        }

        public static Node NewString(string stringValue)
        {
            return new Node(NodeType.String, null, null, stringValue);
        }
    }
}
