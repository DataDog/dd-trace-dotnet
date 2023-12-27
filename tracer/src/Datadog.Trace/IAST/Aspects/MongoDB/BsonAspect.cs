// <copyright file="BsonAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

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

    /*
    /// <summary>
    /// xxxx
    /// </summary>
    /// <param name="json"> json </param>
    /// <returns> oui </returns>
    [AspectMethodInsertBefore("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    public static object Deserialize(string json)
    {
        return json;
    }
    */

    /// <summary>
    /// xxxx
    /// </summary>
    /// <param name="json"> json </param>
    /// <returns> oui </returns>
    [AspectMethodReplace("MongoDB.Bson.BsonDocument::Parse(System.String)")]
    public static object? Parse(string json)
    {
        // Do something with the json
        var taintedJson = IastModule.GetIastContext()?.GetTaintedObjects().Get(json);

        var type = Type.GetType("MongoDB.Bson.BsonDocument, MongoDB.Bson");
        var method = type?.GetMethod("Parse", new Type[] { typeof(string) });
        var result = method?.Invoke(null, new object[] { json });

        // taint the document if the json is tainted
        if (result != null && taintedJson != null)
        {
            var taintedStringReference = taintedJson.PositiveHashCode;
            IastModule.GetIastContext()?.GetTaintedObjects().Taint(result, new Range[] { new Range(0, taintedStringReference) });
        }

        return result;
    }

    /*
    /// <summary>
    /// xxxx
    /// </summary>
    /// <param name="json"> json </param>
    /// <returns> oui </returns>
    [AspectMethodInsertBefore("MongoDB.Bson.IO.JsonReader::.ctor(System.String)")]
    public static object Init(string json)
    {
        return json;
    }*/
}
#endif

