// <copyright file="BsonAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Helpers;
using Datadog.Trace.Iast.Helpers.Reflection;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.MongoDB;

#if !NETFRAMEWORK
/// <summary> BsonAspect class aspect </summary>
[AspectClass("MongoDB.Bson", AspectType.Sink, VulnerabilityType.NoSqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class BsonAspect
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BsonAspect));

    private static readonly FuncWrappers.FuncWrapper<object, object, object> DeserializeExtensionWrapper = new("MongoDB.Bson.Serialization.IBsonSerializerExtensions::Deserialize(MongoDB.Bson.Serialization.IBsonSerializer,MongoDB.Bson.Serialization.BsonDeserializationContext)");
    private static readonly FuncWrappers.FuncWrapper<object, object, object> DeserializeBsonWrapper = new("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(MongoDB.Bson.IO.IBsonReader,System.Action`1[MongoDB.Bson.Serialization.BsonDeserializationContext+Builder])");
    private static readonly FuncWrappers.FuncWrapper<string, object> ParseWrapper = new("MongoDB.Bson.BsonDocument::Parse(System.String)");
    private static readonly FuncWrappers.FuncWrapper<string, object> JsonReaderCtorWrapper = new("MongoDB.Bson.IO.JsonReader::.ctor(System.String)");

    /// <summary>
    ///     MongoDB Deserialize aspect
    /// </summary>
    /// <param name="serializer"> the serializer </param>
    /// <param name="context"> the context </param>
    /// <returns> the original deserialized object </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.IBsonSerializerExtensions::Deserialize(MongoDB.Bson.Serialization.IBsonSerializer`1<!!0>,MongoDB.Bson.Serialization.BsonDeserializationContext)")]
    public static object? DeserializeExtension(object serializer, object context)
    {
        var result = DeserializeExtensionWrapper.Invoke(serializer, context);

        try
        {
            // The reader can be tainted
            var reader = context.GetType().GetProperty("Reader")?.GetValue(context);
            MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedLinkedObject(reader));
        }
        catch (Exception ex) { Log.Debug(ex, "Failed to get the Reader or taint the object"); }

        return result;
    }

    /// <summary>
    ///     MongoDB Deserialize aspect
    /// </summary>
    /// <param name="bsonReader"> the bson reader </param>
    /// <param name="configurator"> the configurator </param>
    /// <returns> the original deserialized object </returns>
    [AspectMethodReplace("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(MongoDB.Bson.IO.IBsonReader,System.Action`1<Builder>)")]
    public static object? DeserializeBson(object bsonReader, object configurator)
    {
        var result = DeserializeBsonWrapper.Invoke(bsonReader, configurator);

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, MongoDbHelper.TaintedLinkedObject(bsonReader));
        }
        catch (Exception ex) { Log.Debug(ex, "Failed to taint the object"); }

        return result;
    }

    /// <summary>
    ///     MongoDB Bson Parse aspect
    /// </summary>
    /// <param name="json"> the json </param>
    /// <returns> the original parsed object </returns>
    [AspectMethodReplace("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    public static object? Parse(string json)
    {
        var result = ParseWrapper.Invoke(json);

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, json);
        }
        catch (Exception ex) { Log.Debug(ex, "Failed to taint the object"); }

        return result;
    }

    /// <summary>
    ///     MongoDB JsonReader constructor aspect
    /// </summary>
    /// <param name="json"> the json </param>
    /// <returns> the JsonReader object </returns>
    [AspectCtorReplace("MongoDB.Bson.IO.JsonReader::.ctor(System.String)")]
    public static object? Constructor(string json)
    {
        var result = JsonReaderCtorWrapper.Invoke(json);

        try
        {
            MongoDbHelper.TaintObjectWithJson(result, json);
        }
        catch (Exception ex) { Log.Debug(ex, "Failed to taint the object"); }

        return result;
    }
}
#endif

