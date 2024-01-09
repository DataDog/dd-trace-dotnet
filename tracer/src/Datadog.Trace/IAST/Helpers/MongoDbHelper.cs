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
        var taintedObjectDocument = IastModule.GetIastContext()?.GetTaintedObjects().Get(bsonCommand.Document);
        if (taintedObjectDocument?.LinkedObject?.Value is not string jsonStringValue)
        {
            return;
        }

        IastModule.OnNoSqlQuery(jsonStringValue, IntegrationId.MongoDb);
    }

    internal static void AnalyzeJsonCommand(object command)
    {
        var jsonCommand = command.DuckCast<JsonCommandStruct>();
        IastModule.OnNoSqlQuery(jsonCommand.Json, IntegrationId.MongoDb);
    }

    // Taint an object by linking it to a tainted string
    internal static void TaintObjectWithJson(object? obj, object? json)
    {
        IastModule.GetIastContext()?.GetTaintedObjects().TaintWithLinkedObject(obj, json);
    }

    internal static object? TaintedLinkedObject(object? taintedObject)
    {
        return taintedObject == null ? null : IastModule.GetIastContext()?.GetTaintedObjects().Get(taintedObject)?.LinkedObject?.Value;
    }
}
