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
    /// MongoDB Bson aspect
    /// </summary>
    /// <param name="source"> source </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.IO.BsonBinaryReader::ReadString()")]
    [AspectMethodReplace("MongoDB.Bson.IO.BsonReader::ReadString()")]
    [AspectMethodReplace("MongoDB.Bson.IO.IBsonReader::ReadString()")]
    public static object ReadString(object source)
    {
        Console.WriteLine(source);
        return "result";
    }

    /// <summary>
    /// test
    /// </summary>
    /// <param name="serializer"> sdf </param>
    /// <param name="context"> sdedf </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.IBsonSerializerExtensions::Deserialize(MongoDB.Bson.Serialization.IBsonSerializer`1<!!0>,MongoDB.Bson.Serialization.BsonDeserializationContext)")]
    public static object? DeserializeExtension(object serializer, object context)
    {
        var result = CallOriginalMethod();

        // The reader can be tainted
        var reader = context.GetType().GetProperty("Reader")?.GetValue(context);
        MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedJsonStringPositiveHashCode(reader));

        return result;

        object? CallOriginalMethod()
        {
            try
            {
                var typeMethodClass = Type.GetType("MongoDB.Bson.Serialization.IBsonSerializerExtensions, MongoDB.Bson")!;
                var typeArgument = Type.GetType("MongoDB.Bson.BsonDocument, MongoDB.Bson")!;
                var methodsPublicStatic = typeMethodClass.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var deserializeMethod = methodsPublicStatic.Where(m => m is { Name: "Deserialize", IsGenericMethod: true }).FirstOrDefault();
                var genericMethod = deserializeMethod?.MakeGenericMethod(typeArgument);

                return genericMethod?.Invoke(null, new object[] { serializer, context });
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// test
    /// </summary>
    /// <param name="bsonReader"> sdgsdg </param>
    /// <param name="configurator"> ezgezggz </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(MongoDB.Bson.IO.IBsonReader,System.Action`1<Builder>)")]
    public static object? DeserializeBson(object bsonReader, object configurator)
    {
        var result = CallOriginalMethod();

        // The reader can be tainted
        MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedJsonStringPositiveHashCode(bsonReader));

        return result;

        object? CallOriginalMethod()
        {
            try
            {
                var typeMethodClass = Type.GetType("MongoDB.Bson.Serialization.BsonSerializer, MongoDB.Bson")!;
                var methodsPublicStatic = typeMethodClass.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var deserializeMethod = methodsPublicStatic.FirstOrDefault(m =>
                                                                               m is { IsGenericMethod: true, Name: "Deserialize" } &&
                                                                               m.GetParameters().Length == 2 &&
                                                                               m.GetParameters()[0].ParameterType == Type.GetType("MongoDB.Bson.IO.IBsonReader, MongoDB.Bson"));

                var genericMethod = deserializeMethod?.MakeGenericMethod(typeof(object));

                return genericMethod?.Invoke(null, new object[] { bsonReader, configurator });
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// xxxx
    /// </summary>
    /// <param name="json"> json </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    public static object? Parse(string json)
    {
        var result = MongoDbHelper.InvokeMethod("MongoDB.Bson.BsonDocument, MongoDB.Bson", "Parse", new object[] { json }, new Type[] { typeof(string) });

        if (result != null && IastModule.GetIastContext()?.GetTaintedObjects().Get(json)?.PositiveHashCode is { } taintedStringReference)
        {
            IastModule.GetIastContext()?.GetTaintedObjects().Taint(result, new Range[] { new Range(0, taintedStringReference) });
        }

        return result;
    }

    /// <summary>
    /// xxxx
    /// </summary>
    /// <param name="json"> json </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.IO.JsonReader::.ctor(System.String)")]
    public static object? Constructor(string json)
    {
        // Invoke the constructor method of the JsonReader
        var typeMethodClass = Type.GetType("MongoDB.Bson.IO.JsonReader, MongoDB.Bson")!;
        var constructor = typeMethodClass.GetConstructor(new Type[] { typeof(string) });
        var result = constructor?.Invoke(new object[] { json });

        MongoDbHelper.TaintObjectWithJson(result, json);

        return result;
    }
}
#endif

