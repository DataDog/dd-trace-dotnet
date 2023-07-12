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
        public static string ToShortString(this object obj)
        {
            Type nominalType = obj.GetType();

            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            var bsonSerializationArgsType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializationArgs, MongoDB.Bson", false);
            object args = Activator.CreateInstance(bsonSerializationArgsType);
            bsonSerializationArgsType.GetProperty("NominalType")?.SetValue(args, nominalType);

            Type bsonSerializerType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializer, MongoDB.Bson");
            MethodInfo lookupSerializerMethod = bsonSerializerType?.GetMethod("LookupSerializer", new Type[] { typeof(Type) });

            object serializer = lookupSerializerMethod.Invoke(null, new[] { nominalType });

            using (var stringWriter = new StringWriter())
            {
                Type typeJsonWriterSettings = Type.GetType("MongoDB.Bson.IO.JsonWriterSettings, MongoDB.Bson", throwOnError: false);
                Type[] types = { typeof(StringWriter), typeJsonWriterSettings };

                Type typeJsonWriter = Type.GetType("MongoDB.Bson.IO.JsonWriter, MongoDB.Bson", throwOnError: false);
                ConstructorInfo constructorJsonWriter = typeJsonWriter?.GetConstructor(types);
                var defaultsValueJsonWriterSettings = new object[] { (TextWriter)stringWriter, typeJsonWriterSettings?.GetProperty("Defaults")?.GetValue(null) };

                var bsonWriter = constructorJsonWriter?.Invoke(defaultsValueJsonWriterSettings);

                Type contextType = Type.GetType("MongoDB.Bson.Serialization.BsonSerializationContext, MongoDB.Bson", throwOnError: false);
                MethodInfo createRootMethod = contextType?.GetMethod("CreateRoot", BindingFlags.Static | BindingFlags.Public);

                var rootContext = createRootMethod?.Invoke(null, new[] { bsonWriter, null });

                Type bsonSerializerInterfaceType = Type.GetType("MongoDB.Bson.Serialization.IBsonSerializer, MongoDB.Bson", throwOnError: false);
                MethodInfo serializeMethod = bsonSerializerInterfaceType?.GetMethod("Serialize");

                serializeMethod?.Invoke(serializer, new[] { rootContext, args, obj });

                return stringWriter.ToString();
            }
        }
    }
}
