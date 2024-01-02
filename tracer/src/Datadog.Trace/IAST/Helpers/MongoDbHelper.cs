// <copyright file="MongoDbHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Iast.Helpers;

internal static class MongoDbHelper
{
    internal static void AnalyzeBsonDocument(object command)
    {
        try
        {
            var document = command.GetType().GetProperty("Document")?.GetValue(command);
            if (document == null)
            {
                return;
            }

            var taintedObjectDocument = IastModule.GetIastContext()?.GetTaintedObjects().Get(document);
            if (taintedObjectDocument?.LinkedObject?.Value is not string jsonStringValue)
            {
                return;
            }

            IastModule.OnNoSqlQuery(jsonStringValue, IntegrationId.MongoDb);
        }
        catch (Exception)
        {
            // Failed to get Document property, ignore
        }
    }

    internal static void AnalyzeJsonCommand(object command)
    {
        try
        {
            var json = command.GetType().GetProperty("Json")?.GetValue(command);
            if (json is not string jsonStringValue)
            {
                return;
            }

            IastModule.OnNoSqlQuery(jsonStringValue, IntegrationId.MongoDb);
        }
        catch (Exception)
        {
            // Failed to get Json property, ignore
        }
    }

    internal static object? InvokeMethod(string typeName, string methodName, object[] args, Type[] argTypes)
    {
        try
        {
            var type = Type.GetType(typeName);
            var method = type?.GetMethod(methodName, argTypes);
            var result = method?.Invoke(null, args);
            return result;
        }
        catch (Exception)
        {
            return null;
        }
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
