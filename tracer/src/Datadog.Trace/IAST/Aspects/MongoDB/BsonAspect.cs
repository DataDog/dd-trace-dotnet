// <copyright file="BsonAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Helpers;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.MongoDB;

#if !NETFRAMEWORK
/// <summary> BsonAspect class aspect </summary>
[AspectClass("MongoDB.Bson", AspectType.Sink, VulnerabilityType.NoSqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class BsonAspect
{
    /// <summary>
    /// MongoDB Deserialize aspect
    /// </summary>
    /// <param name="serializer"> the serializer </param>
    /// <param name="context"> the context </param>
    /// <returns> the original deserialized object </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.IBsonSerializerExtensions::Deserialize(MongoDB.Bson.Serialization.IBsonSerializer`1<!!0>,MongoDB.Bson.Serialization.BsonDeserializationContext)")]
    public static object? DeserializeExtension(object serializer, object context)
    {
        var result = CallOriginalMethod();

        try
        {
            // The reader can be tainted
            var reader = context.GetType().GetProperty("Reader")?.GetValue(context);
            MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedLinkedObject(reader));
        }
        catch (Exception) { /* Failed to get the Reader or taint the object */ }

        return result;

        object? CallOriginalMethod()
        {
            var typeMethodClass = Type.GetType("MongoDB.Bson.Serialization.IBsonSerializerExtensions, MongoDB.Bson")!;
            var methodsPublicStatic = typeMethodClass.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var deserializeMethod = methodsPublicStatic.Where(m => m is { Name: "Deserialize" }).FirstOrDefault();
            return deserializeMethod?.Invoke(null, new[] { serializer, context });
        }
    }

    /// <summary>
    /// MongoDB Deserialize aspect
    /// </summary>
    /// <param name="bsonReader"> the bson reader </param>
    /// <param name="configurator"> the configurator </param>
    /// <returns> the original deserialized object </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(MongoDB.Bson.IO.IBsonReader,System.Action`1<Builder>)")]
    public static object? DeserializeBson(object bsonReader, object configurator)
    {
        var result = CallOriginalMethod();

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedLinkedObject(bsonReader));
        }
        catch (Exception) { /* Failed to taint the object */ }

        return result;

        object? CallOriginalMethod()
        {
            var typeMethodClass = Type.GetType("MongoDB.Bson.Serialization.BsonSerializer, MongoDB.Bson")!;
            var methodsPublicStatic = typeMethodClass.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var deserializeMethod = methodsPublicStatic.FirstOrDefault(m =>
                                                                               m is { IsGenericMethod: true, Name: "Deserialize" } &&
                                                                               m.GetParameters().Length == 2 &&
                                                                               m.GetParameters()[0].ParameterType == Type.GetType("MongoDB.Bson.IO.IBsonReader, MongoDB.Bson"));

            var genericMethod = deserializeMethod?.MakeGenericMethod(typeof(object));

            return genericMethod?.Invoke(null, new[] { bsonReader, configurator });
        }
    }

    /// <summary>
    /// MongoDB Bson Parse aspect
    /// </summary>
    /// <param name="json"> the json </param>
    /// <returns> the original parsed object </returns>
    [AspectMethodReplace("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    public static object? Parse(string json)
    {
        var result = MongoDbHelper.InvokeMethod("MongoDB.Bson.BsonDocument, MongoDB.Bson", "Parse", new object[] { json }, new[] { typeof(string) });

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, json);
        }
        catch (Exception) { /* Failed to taint the object */ }

        return result;
    }

    /// <summary>
    /// MongoDB JsonReader constructor aspect
    /// </summary>
    /// <param name="json"> the json </param>
    /// <returns> the JsonReader object </returns>
    [AspectCtorReplace("MongoDB.Bson.IO.JsonReader::.ctor(System.String)")]
    public static object? Constructor(string json)
    {
        // Invoke the constructor method of the JsonReader
        var typeMethodClass = Type.GetType("MongoDB.Bson.IO.JsonReader, MongoDB.Bson")!;
        var constructor = typeMethodClass.GetConstructor(new[] { typeof(string) });
        var result = constructor?.Invoke(new object[] { json });

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, json);
        }
        catch (Exception) { /* Failed to taint the object */ }

        return result;
    }
}
#endif

