// <copyright file="BsonExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal static class BsonExtensions
    {
        public static string ToJson(
           this object obj,
           Type nominalType,
           object writerSettings = null,
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

            Type type = Type.GetType("MongoDB.Bson.IO.JsonWriterSettings, MongoDB.Bson", throwOnError: false);
            PropertyInfo defaultsProperty = type.GetProperty("Defaults");
            object defaultsValue = defaultsProperty.GetValue(null);

            Console.WriteLine("defaultsValue:" + defaultsValue);
            Console.WriteLine("JsonWriterSettings.Defaults:" + JsonWriterSettings.Defaults);

            using (var stringWriter = new StringWriter())
            {
                using (var bsonWriter = new CompactJsonWriter(stringWriter, writerSettings ?? defaultsValue))
                {
                    var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
                    serializer.Serialize(context, args, obj);
                }

                return stringWriter.ToString();
            }
        }

        internal static object ReflectObject(string typeName,  string methodName)
        {
            var returnedType = Type.GetType(typeName, throwOnError: false);

            if (typeName == null)
            {
                return null;
            }

            var returnedMethod = returnedType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            return returnedMethod.Invoke(null, null);
        }
    }
}
