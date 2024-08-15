// <copyright file="BsonAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Helpers;

namespace Datadog.Trace.Iast.Aspects.MongoDB;

/// <summary> BsonAspect class aspect </summary>
[AspectClass("MongoDB.Bson", AspectType.Sink, VulnerabilityType.NoSqlMongoDbInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class BsonAspect
{
    /// <summary>
    ///     MongoDB Bson Parse aspect
    /// </summary>
    /// <param name="json"> the json </param>
    /// <returns> the original parsed object </returns>
    [AspectMethodInsertBefore("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(System.String,System.Action`1<Builder>)", 1)]
    [AspectMethodInsertBefore("MongoDB.Bson.Serialization.BsonSerializer::Deserialize(System.String,System.Type,System.Action`1<Builder>)", 2)]
    [AspectMethodInsertBefore("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    [AspectMethodInsertBefore("MongoDB.Bson.IO.JsonReader::.ctor(System.String)")]
    public static object AnalyzeJsonString(string json)
    {
        try
        {
            IastModule.OnNoSqlMongoDbQuery(json, IntegrationId.MongoDb);
            return json;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(BsonAspect)}.{nameof(AnalyzeJsonString)}");
            return json;
        }
    }
}
