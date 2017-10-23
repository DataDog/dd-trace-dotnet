using MsgPack;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Tracer.IntegrationTests
{
    /// <summary>
    /// This class provides a bunch of helpers to read Span and ServiceInfo data
    /// from their serialized MsgPack representation. (It is not straightfoward
    /// to create deserializer for them since they are not public and don't provide
    /// setters for all fields)
    /// </summary>
    public static class MsgPackHelpers
    {
        public static ulong TraceId(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["trace_id"].AsUInt64();
        }

        public static ulong SpanId(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["span_id"].AsUInt64();
        }

        public static ulong ParentId(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["parent_id"].AsUInt64();
        }

        public static string OperationName(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["name"].AsString();
        }
        public static string ResourceName(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["resource"].AsString();
        }

        public static string ServiceName(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["service"].AsString();
        }

        public static long StartTime(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["start"].AsInt64();
        }
        public static long Duration(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["duration"].AsInt64();
        }

        public static string Type(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["type"].AsString();
        }

        public static string Error(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["error"].AsString();
        }

        public static Dictionary<string, string> Tags(this MessagePackObject obj)
        {
            return obj.AsList().First().AsDictionary()["meta"].AsDictionary().ToDictionary(kv => kv.Key.AsString(), kv => kv.Value.AsString());
        }

        public static IEnumerable<ServiceInfo> ServiceInfos(this MessagePackObjectDictionary obj)
        {
            foreach(var kv in obj)
            {
                var s = kv.Value.AsDictionary();
                yield return new ServiceInfo { App = s["app"].AsString(), AppType = s["app_type"].AsString(), ServiceName = kv.Key.AsString() };
            }
        }
    }
}
