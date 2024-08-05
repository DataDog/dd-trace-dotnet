// <copyright file="MongoDatabaseAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects.MongoDB;

/// <summary> MongoDB Driver class aspect </summary>
[AspectClass("MongoDB.Driver", AspectType.Sink, VulnerabilityType.NoSqlMongoDbInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class MongoDatabaseAspect
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MongoDatabaseAspect));

    /// <summary>
    ///     MongoDB Driver aspect
    /// </summary>
    /// <param name="command"> the mongodb command </param>
    /// <returns> the original command </returns>
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoDatabase::RunCommand(MongoDB.Driver.Command`1<!!0>,MongoDB.Driver.ReadPreference,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoDatabase::RunCommandAsync(MongoDB.Driver.Command`1<!!0>,MongoDB.Driver.ReadPreference,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoCollectionExtensions::Find(MongoDB.Driver.IMongoCollection`1<!!0>,MongoDB.Driver.FilterDefinition`1<!!0>,MongoDB.Driver.FindOptions)", 1)]
    [AspectMethodInsertBefore("MongoDB.Driver.IMongoCollectionExtensions::FindAsync(MongoDB.Driver.IMongoCollection`1<!!0>,MongoDB.Driver.FilterDefinition`1<!!0>,MongoDB.Driver.FindOptions`2<!!0,!!0>,System.Threading.CancellationToken)", 2)]
    public static object? AnalyzeCommand(object? command)
    {
        try
        {
            if (command == null || !Iast.Instance.Settings.Enabled)
            {
                return command;
            }

            var commandType = command.GetType().Name;
            switch (commandType)
            {
                case "BsonDocumentFilterDefinition`1":
                case "BsonDocumentCommand`1":
                    MongoDbHelper.AnalyzeBsonDocument(command);
                    break;
                case "JsonFilterDefinition`1":
                case "JsonCommand`1":
                    MongoDbHelper.AnalyzeJsonCommand(command);
                    break;
            }

            return command;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Warning(ex, $"Error invoking {nameof(MongoDatabaseAspect)}.{nameof(AnalyzeCommand)}");
            return command;
        }
    }
}
