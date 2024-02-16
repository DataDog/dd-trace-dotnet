// <copyright file="MongoDbHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

namespace Datadog.Trace.Iast.Helpers;

internal static class MongoDbHelper
{
    internal static void AnalyzeBsonDocument(object command)
    {
        var bsonCommand = command.DuckCast<BsonDocumentCommandStruct>();
        var jsonString = bsonCommand.Document.ToString();
        if (string.IsNullOrEmpty(jsonString))
        {
            return;
        }

        IastModule.OnNoSqlMongoDbQuery(jsonString, IntegrationId.MongoDb);
    }

    internal static void AnalyzeJsonCommand(object command)
    {
        var jsonCommand = command.DuckCast<JsonCommandStruct>();
        IastModule.OnNoSqlMongoDbQuery(jsonCommand.Json, IntegrationId.MongoDb);
    }
}
