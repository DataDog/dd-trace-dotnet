using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using MsgPack;
using Xunit;

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

        public static void AssertSpanEqual(Span expected, MessagePackObject actual)
        {
            Assert.Equal(expected.Context.TraceId, actual.TraceId());
            Assert.Equal(expected.Context.SpanId, actual.SpanId());
            if (expected.Context.ParentId.HasValue)
            {
                Assert.Equal(expected.Context.ParentId, actual.ParentId());
            }

            Assert.Equal(expected.OperationName, actual.OperationName());
            Assert.Equal(expected.ResourceName, actual.ResourceName());
            Assert.Equal(expected.ServiceName, actual.ServiceName());
            Assert.Equal(expected.Type, actual.Type());
            Assert.Equal(expected.StartTime.ToUnixTimeNanoseconds(), actual.StartTime());
            Assert.Equal(expected.Duration.ToNanoseconds(), actual.Duration());
            if (expected.Error)
            {
                Assert.Equal("1", actual.Error());
            }

            if (expected.Tags != null)
            {
                Assert.Equal(expected.Tags, actual.Tags());
            }
        }

        private static MessagePackObjectDictionary FirstDictionary(this MessagePackObject obj)
        {
            if (obj.IsList)
            {
                return obj.AsList().First().FirstDictionary();
            }

            return obj.AsDictionary();
        }
    }
}
