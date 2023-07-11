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
        public static string ToShortString(this object obj, object args = default)
        {
            Type nominalType = obj.GetType();

            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            PropertyInfo nominalTypeProperty = args?.GetType().GetProperty("NominalType");

            nominalTypeProperty?.SetValue(args, nominalType);

            Type bsonSerializerType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializer, MongoDB.Bson");
            MethodInfo lookupSerializerMethod = bsonSerializerType?.GetMethod("LookupSerializer", new Type[] { typeof(Type) });

            Type bsonDocumentType = Type.GetType("MongoDB.Bson.BsonDocument, MongoDB.Bson");

            object serializer = null;

            if (serializer == null)
            {
                serializer = lookupSerializerMethod.Invoke(null, new[] { bsonDocumentType });
            }

            using (var stringWriter = new StringWriter())
            {
                object writerSettings = null;

                Type typeJsonWriterSettings = Type.GetType("MongoDB.Bson.IO.JsonWriterSettings, MongoDB.Bson", throwOnError: false);

                Type[] types = { typeof(StringWriter), typeJsonWriterSettings };

                Type typeJsonWriter = Type.GetType("MongoDB.Bson.IO.JsonWriter, MongoDB.Bson", throwOnError: false);
                ConstructorInfo constructorJsonWriter = typeJsonWriter?.GetConstructor(types);

                var defaultsValueJsonWriterSettings = new[] { (TextWriter)stringWriter, typeJsonWriterSettings?.GetProperty("Defaults")?.GetValue(writerSettings) };
                var bsonWriter = constructorJsonWriter?.Invoke(defaultsValueJsonWriterSettings);

                Type contextType = Type.GetType("MongoDB.Bson.BsonSerializationContext, MongoDB.Bson", throwOnError: false);
                MethodInfo createRootMethod = contextType?.GetMethod("CreateRoot", BindingFlags.Static | BindingFlags.Public);

                object configurator = null;
                var context = createRootMethod?.Invoke(null, new[] { bsonWriter, configurator });

                Type bsonSerializerInterfaceType = Type.GetType("MongoDB.Bson.Serialization.IBsonSerializer, MongoDB.Bson", throwOnError: false);
                MethodInfo serializeMethod = bsonSerializerInterfaceType?.GetMethod("Serialize");

                object castedObj = Convert.ChangeType(obj, bsonDocumentType);
                serializeMethod?.Invoke(serializer, new[] { context, args, castedObj });

                return stringWriter.ToString();
            }
        }
    }
}
