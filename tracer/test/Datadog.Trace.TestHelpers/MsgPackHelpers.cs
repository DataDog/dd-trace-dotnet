// <copyright file="MsgPackHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using MsgPack;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// This class provides a bunch of helpers to read Span and ServiceInfo data
    /// from their serialized MsgPack representation. (It is not straightforward
    /// to create deserializer for them since they are not public and don't provide
    /// setters for all fields)
    /// </summary>
    public static class MsgPackHelpers
    {
        public static ulong TraceId(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["trace_id"].AsUInt64();
        }

        public static ulong SpanId(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["span_id"].AsUInt64();
        }

        public static ulong ParentId(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["parent_id"].AsUInt64();
        }

        public static string OperationName(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["name"].AsString();
        }

        public static string ResourceName(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["resource"].AsString();
        }

        public static string ServiceName(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["service"].AsString();
        }

        public static long StartTime(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["start"].AsInt64();
        }

        public static long Duration(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["duration"].AsInt64();
        }

        public static string Type(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["type"].AsString();
        }

        public static string Error(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["error"].AsString();
        }

        public static Dictionary<string, string> Tags(this MessagePackObject obj)
        {
            return obj.FirstDictionary()["meta"].AsDictionary().ToDictionary(kv => kv.Key.AsString(), kv => kv.Value.AsString());
        }

        public static MessagePackObjectDictionary FirstDictionary(this MessagePackObject obj)
        {
            if (obj.IsList)
            {
                return obj.AsList().First().FirstDictionary();
            }

            return obj.AsDictionary();
        }
    }
}
