// <copyright file="BsonExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class BsonExtensions
    {
        public static string ToJson(
           this object obj,
           Type nominalType,
           JsonWriterSettings writerSettings = null,
           IBsonSerializer serializer = null,
           Action<BsonSerializationContext.Builder> configurator = null,
           BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            args.NominalType = nominalType;

            if (serializer == null)
            {
                serializer = BsonSerializer.LookupSerializer(nominalType);
            }

            if (serializer.ValueType != nominalType)
            {
                var message = string.Format("Serializer type {0} value type does not match document types {1}.", serializer.GetType().FullName, nominalType.FullName);
                throw new ArgumentException(message, "serializer");
            }

            using (var stringWriter = new StringWriter())
            {
                using (var bsonWriter = new ClrProfiler.AutoInstrumentation.MongoDb.CompactJsonWriter(stringWriter, writerSettings ?? JsonWriterSettings.Defaults))
                {
                    var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
                    serializer.Serialize(context, args, obj);
                }

                return stringWriter.ToString();
            }
        }
    }
}
