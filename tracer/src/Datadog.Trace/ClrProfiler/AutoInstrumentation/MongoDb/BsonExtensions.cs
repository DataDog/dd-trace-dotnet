// <copyright file="BsonExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal static class BsonExtensions
    {
        public static string ToJson(
           this object obj,
           Type nominalType,
           object writerSettings = null,
           object serializer = null,
           object configurator = null,
           object args = default(object))
        {
            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            PropertyInfo nominalTypeProperty = args?.GetType().GetProperty("NominalType");
            nominalTypeProperty?.SetValue(args, nominalType);

            Type bsonSerializerType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializer, MongoDB.Bson");
            var lookupSerializerMethod = bsonSerializerType?.GetMethod("LookupSerializer", BindingFlags.Public | BindingFlags.Static);

            if (serializer == null)
            {
                serializer = lookupSerializerMethod?.Invoke(null, new object[] { nominalType });
            }

            if ((Type)bsonSerializerType.GetProperty("ValueType").GetValue(serializer) != nominalType)
            {
                var message = string.Format("Serializer type {0} value type does not match document types {1}.", serializer.GetType().FullName, nominalType.FullName);
                throw new ArgumentException(message, "serializer");
            }

            using (var stringWriter = new StringWriter())
            {
                Type typeJsonWriterSettings = Type.GetType("MongoDB.Bson.IO.JsonWriterSettings, MongoDB.Bson", throwOnError: false);
                var defaultsValueJsonWriterSettings = new[] { typeJsonWriterSettings?.GetProperty("Defaults")?.GetValue(writerSettings) };

                Type[] types = { typeof(StringWriter), typeJsonWriterSettings };

                Type typeJsonWriter = Type.GetType("MongoDB.Bson.IO.JsonWriter, MongoDB.Bson", throwOnError: false);
                ConstructorInfo constructorJsonWriter = typeJsonWriter?.GetConstructor(types);

                var bsonWriter = constructorJsonWriter?.Invoke(stringWriter, defaultsValueJsonWriterSettings);

                var contextType = Type.GetType("MongoDB.Bson.BsonSerializationContext, MongoDB.Bson", throwOnError: false);
                var createRootMethod = contextType?.GetMethod("CreateRoot", BindingFlags.Static | BindingFlags.Public);

                var context = createRootMethod?.Invoke(null, new object[] { bsonWriter, configurator });
                var serializeMethod = bsonSerializerType?.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static);

                serializeMethod?.Invoke(serializer, new[] { context, args, obj });

                return stringWriter.ToString();
            }
        }
    }
}
