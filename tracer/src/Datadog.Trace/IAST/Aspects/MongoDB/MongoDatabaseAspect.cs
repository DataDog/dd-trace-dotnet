// <copyright file="MongoDatabaseAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.MongoDB;

/// <summary> BsonAspect class aspect </summary>
[AspectClass("MongoDB.Driver", AspectType.Sink, VulnerabilityType.NoSqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class MongoDatabaseAspect
{
    /// <summary>
    /// xx
    /// </summary>
    /// <param name="command"> command </param>
    /// <returns> oui </returns>
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoDatabase::RunCommand(MongoDB.Driver.Command`1<!!0>,MongoDB.Driver.ReadPreference,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoDatabase::RunCommandAsync(MongoDB.Driver.Command`1<!!0>,MongoDB.Driver.ReadPreference,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoCollectionExtensions::Find(MongoDB.Driver.IMongoCollection`1<!!0>,MongoDB.Driver.FilterDefinition`1<!!0>,MongoDB.Driver.FindOptions)", 1)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoCollectionExtensions::FindAsync(MongoDB.Driver.IMongoCollection`1<!!0>,MongoDB.Driver.FilterDefinition`1<!!0>,MongoDB.Driver.FindOptions`2<!!0,!!0>,System.Threading.CancellationToken)", 2)]
    public static object AnalyzeCommand(object command)
    {
        // Check if the command is a BsonDocument
        var commandType = command.GetType().Name;
        switch (commandType)
        {
            case "BsonDocumentFilterDefinition`1":
            case "BsonDocumentCommand`1":
                AnalyzeBsonDocument(command);
                break;
            case "JsonFilterDefinition`1":
            case "JsonCommand`1":
                AnalyzeJsonCommand(command);
                break;
        }

        return command;

        static void AnalyzeBsonDocument(object command)
        {
            var document = command.GetType().GetProperty("Document")?.GetValue(command);
            if (document == null)
            {
                return;
            }

            var taintedObjectDocument = IastModule.GetIastContext()?.GetTaintedObjects().Get(document);
            if (taintedObjectDocument is null)
            {
                return;
            }

            var jsonTaintedObject = IastModule.GetIastContext()?.GetTaintedObjects().FromPositiveHashCode(taintedObjectDocument.Ranges[0].Length);
            if (jsonTaintedObject?.Value is not string jsonStringValue)
            {
                return;
            }

            IastModule.OnNoSqlQuery(jsonStringValue, IntegrationId.MongoDb);
        }

        static void AnalyzeJsonCommand(object command)
        {
            var json = command.GetType().GetProperty("Json")?.GetValue(command);
            if (json is not string jsonStringValue)
            {
                return;
            }

            IastModule.OnNoSqlQuery(jsonStringValue, IntegrationId.MongoDb);
        }
    }
}
