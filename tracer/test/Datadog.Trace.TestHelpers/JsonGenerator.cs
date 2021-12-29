// <copyright file="JsonGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Bogus;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.TestHelpers
{
    public class JsonGenerator
    {
        private static readonly JTokenType[] OpeningTypes = new[] { JTokenType.Array, JTokenType.Object };
        private static readonly JTokenType[] TerminatingTypes = new[] { JTokenType.Integer, JTokenType.Float, JTokenType.String, JTokenType.Boolean, JTokenType.Null, JTokenType.Date, JTokenType.Guid, JTokenType.Uri, JTokenType.TimeSpan };
        private readonly Random _rnd = new();
        private readonly Faker faker = new("en");

        public ArraySegment<byte> GenerateJsonBuffer()
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);
            using var writer = new JsonTextWriter(streamWriter);

            WriteOpeningValue(writer, 0);

            writer.Flush();
            streamWriter.Flush();
            memoryStream.Position = 0;

            return new ArraySegment<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }

        private void WriteValue(JsonTextWriter writer, int depth)
        {
            var writeComment = _rnd.Next(6);
            if (writeComment == 0)
            {
                writer.WriteComment(faker.Random.Utf16String(0, 100));
            }

            var valueType = _rnd.Next(2);
            if (depth >= 6 || valueType == 0)
            {
                WriteTerminatingValue(writer);
            }
            else
            {
                WriteOpeningValue(writer, depth);
            }
        }

        private void WriteObject(JsonTextWriter writer, int depth)
        {
            writer.WriteStartObject();

            var properties = _rnd.Next(15);

            for (var i = 0; i < properties; i++)
            {
                writer.WritePropertyName(faker.Random.Utf16String(4, 16));
                WriteValue(writer, depth);
            }

            writer.WriteEndObject();
        }

        private void WriteArray(JsonTextWriter writer, int depth)
        {
            writer.WriteStartArray();

            var values = _rnd.Next(15);

            for (var i = 0; i < values; i++)
            {
                WriteValue(writer, depth);
            }

            writer.WriteEndArray();
        }

        private void WriteTerminatingValue(JsonTextWriter writer)
        {
            var i = _rnd.Next(TerminatingTypes.Length);
            var valueType = TerminatingTypes[i];
            switch (valueType)
            {
                case JTokenType.Integer:
                    writer.WriteValue(faker.Random.Int());
                    break;
                case JTokenType.Float:
                    writer.WriteValue(faker.Random.Float());
                    break;
                case JTokenType.String:
                    writer.WriteValue(faker.Random.Utf16String(0, 100));
                    break;
                case JTokenType.Boolean:
                    writer.WriteValue(faker.Random.Bool());
                    break;
                case JTokenType.Null:
                    writer.WriteNull();
                    break;
                case JTokenType.Date:
                    writer.WriteValue(faker.Date.Between(DateTime.MinValue, DateTime.MaxValue));
                    break;
                case JTokenType.Guid:
                    writer.WriteValue(Guid.NewGuid());
                    break;
                case JTokenType.Uri:
                    writer.WriteValue(faker.Internet.Url());
                    break;
                case JTokenType.TimeSpan:
                    var timeSpan = DateTime.MaxValue - faker.Date.Between(DateTime.MinValue, DateTime.MaxValue);
                    writer.WriteValue(timeSpan);
                    break;
                default:
                    Assert.False(true);
                    break;
            }
        }

        private void WriteOpeningValue(JsonTextWriter writer, int depth)
        {
            depth++;

            var i = _rnd.Next(OpeningTypes.Length);
            var valueType = OpeningTypes[i];
            switch (valueType)
            {
                case JTokenType.Object:
                    WriteObject(writer, depth);
                    break;
                case JTokenType.Array:
                    WriteArray(writer, depth);
                    break;
                default:
                    Assert.False(true);
                    break;
            }
        }
    }
}
