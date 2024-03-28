// <copyright file="TaintVisitor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.Iast.Helpers;

#nullable enable

internal class TaintVisitor
{
    private readonly HashSet<object> _visited = new();
    private readonly int _maxDepth;
    private readonly int _maxVisitedObjects;
    private readonly TaintedObjects _taintedObjects;

    private TaintVisitor(int maxDepth, int maxVisitedObjects, TaintedObjects taintedObjects)
    {
        _maxDepth = maxDepth;
        _maxVisitedObjects = maxVisitedObjects;
        _taintedObjects = taintedObjects;
    }

    public static void Visit(object? obj, int maxDepth, int maxVisitedObjects)
    {
        var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
        if (taintedObjects == null)
        {
            return;
        }

        new TaintVisitor(maxDepth, maxVisitedObjects, taintedObjects).Visit(obj);
    }

    private void Visit(object? obj, int currentDepth = 0)
    {
        if (obj == null || _visited.Contains(obj) || currentDepth > _maxDepth || _visited.Count >= _maxVisitedObjects)
        {
            return;
        }

        _visited.Add(obj);

        var type = obj.GetType();
        if (type.IsPrimitive)
        {
            return;
        }

        if (type == typeof(string))
        {
            Taint(obj);
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(string))
            {
                Taint(property.GetValue(obj));
            }
            else if (!property.PropertyType.IsPrimitive)
            {
                Visit(property.GetValue(obj), currentDepth + 1);
            }
        }
    }

    private void Taint(object? value)
    {
        if (value is not string valueStr)
        {
            return;
        }

        _taintedObjects.TaintInputString(valueStr, new Source(SourceType.RequestBody, valueStr, valueStr));
    }
}
